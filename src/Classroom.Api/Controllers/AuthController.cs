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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailService _emailService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwt,
        IEmailService emailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _jwt = jwt;
        _emailService = emailService;
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

        return Ok(new { user.Id, user.Email, user.FullName, Role = role });
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

        var ok = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!ok.Succeeded)
            return Unauthorized("Invalid credentials.");

        var (token, expiresAt) = await _jwt.CreateAsync(user);
        return Ok(new AuthResponse(token, expiresAt));
    }

    // POST api/v1/auth/forgot-password
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
        // Send email (service will url-encode token)
        await _emailService.SendPasswordResetAsync(user.Email, token);

        return Ok();
    }

    // POST api/v1/auth/reset-password
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

        // Token should arrive URL-decoded by client; ensure safe decoding here
        var decodedToken = WebUtility.UrlDecode(req.Token);

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok();
    }
    }
