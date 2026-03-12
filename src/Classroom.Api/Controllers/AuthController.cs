using Classroom.Application.DTOs;
using Classroom.Domain.Enums;
using Classroom.Infrastructure.Auth;
using Classroom.Infrastructure.Email;
using Classroom.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private const string TeacherEmailDomain = "@parktownboys.com";
    private static readonly TimeSpan TeacherVerifyCodeTtl = TimeSpan.FromMinutes(15);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailService _emailService;
    private readonly IAdmissionsValidator _admissions;
    private readonly ITeacherEmailVerificationStore _teacherVerifyStore;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwt,
        IEmailService emailService,
        IAdmissionsValidator admissions,
        ITeacherEmailVerificationStore teacherVerifyStore)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _jwt = jwt;
        _emailService = emailService;
        _admissions = admissions;
        _teacherVerifyStore = teacherVerifyStore;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email and password are required.");

        var role = req.Role?.Trim();
        if (role is not (AppRole.SuperAdmin or AppRole.Teacher or AppRole.Learner))
            return BadRequest("Role must be SuperAdmin, Teacher, or Learner.");

        if (role == AppRole.Teacher)
        {
            var email = req.Email.Trim();
            if (!email.EndsWith(TeacherEmailDomain, StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Teacher email address must end with '{TeacherEmailDomain}'.");
        }

        if (role == AppRole.Learner)
        {
            if (string.IsNullOrWhiteSpace(req.AdminId))
                return BadRequest("AdminId (admission number) is required for Learner registration.");

            var ok = await _admissions.IsValidAsync(req.AdminId, HttpContext.RequestAborted);
            if (!ok)
                return BadRequest($"AdminId '{req.AdminId}' was not found in the admissions list. Learner registration is not allowed.");
        }

        if (!await _roleManager.RoleExistsAsync(role))
            await _roleManager.CreateAsync(new IdentityRole(role));

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            AdminId = req.AdminId
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, role);

        if (role == AppRole.Teacher)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = _teacherVerifyStore.Issue(user.Email!, token, TeacherVerifyCodeTtl);

            await _emailService.SendEmailConfirmationCodeAsync(user.Email!, code, HttpContext.RequestAborted);

            return Accepted(new
            {
                message = "Account created. Enter the verification code sent to your email to activate your Teacher account.",
                email = user.Email
            });
        }

        return Ok(new { user.Id, user.Email, user.FullName, Role = role });
    }

    [AllowAnonymous]
    [HttpPost("confirm-email-code")]
    public async Task<IActionResult> ConfirmEmailCode([FromBody] ConfirmEmailCodeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest("Email and code are required.");

        var user = await _userManager.FindByEmailAsync(req.Email.Trim());
        if (user is null) return BadRequest("Invalid request.");

        if (!_teacherVerifyStore.TryConsume(req.Email, req.Code, out var identityToken))
            return BadRequest("Invalid or expired verification code.");

        var result = await _userManager.ConfirmEmailAsync(user, identityToken);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new { message = "Email verified. Your account is now active." });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        ApplicationUser? user = null;

        if (!string.IsNullOrWhiteSpace(req.Email))
            user = await _userManager.FindByEmailAsync(req.Email);

        if (user is null && !string.IsNullOrWhiteSpace(req.AdminId))
        {
            user = await _userManager.Users.FirstOrDefaultAsync(u => u.AdminId == req.AdminId);
            if (user is null) return Unauthorized("Invalid credentials.");
            if (!await _userManager.IsInRoleAsync(user, AppRole.Learner))
                return Unauthorized("AdminId login allowed only for Learner accounts.");
        }

        if (user is null)
            return Unauthorized("Invalid credentials.");

        if (await _userManager.IsInRoleAsync(user, AppRole.Teacher) && !user.EmailConfirmed)
            return Unauthorized("Please verify your email using the verification code before logging in.");

        var ok = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!ok.Succeeded)
            return Unauthorized("Invalid credentials.");

        var (token, expiresAt) = await _jwt.CreateAsync(user);
        return Ok(new AuthResponse(token, expiresAt));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Email))
            return BadRequest("Email is required.");

        var user = await _userManager.FindByEmailAsync(req.Email);

        // Do not reveal whether the email exists
        if (user is null)
            return Ok();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _emailService.SendPasswordResetAsync(user.Email!, token, HttpContext.RequestAborted);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Email) ||
            string.IsNullOrWhiteSpace(req?.Token) ||
            string.IsNullOrWhiteSpace(req?.NewPassword))
            return BadRequest("Email, token and new password are required.");

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null) return BadRequest("Invalid request.");

        var decodedToken = WebUtility.UrlDecode(req.Token);
        var result = await _userManager.ResetPasswordAsync(user, decodedToken, req.NewPassword);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok();
    }
}
