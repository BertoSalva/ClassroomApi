using Classroom.Application.DTOs;
using Classroom.Domain.Enums;
using Classroom.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = AppRole.SuperAdmin)]
public sealed class UsersController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        AppRole.SuperAdmin,
        AppRole.Teacher,
        AppRole.Learner
    };

    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public UsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    // GET /api/v1/users
    [HttpGet]
    public async Task<ActionResult<List<UserWithRolesDto>>> GetAll(CancellationToken ct)
    {
        // Materialize first to avoid multiple DB queries while enumerating IQueryable concurrently
        var users = _users.Users
            .OrderBy(u => u.Email)
            .ToList();

        var result = new List<UserWithRolesDto>(users.Count);

        foreach (var u in users)
        {
            ct.ThrowIfCancellationRequested();

            var roles = await _users.GetRolesAsync(u);

            result.Add(new UserWithRolesDto(
                u.Id,
                u.Email,
                u.FullName,
                u.AdminId,
                roles
            ));
        }

        return Ok(result);
    }

    // PUT /api/v1/users/roles
    // Body: { "userId": "...", "roles": ["Teacher"] }
    [HttpPut("roles")]
    public async Task<ActionResult<UserWithRolesDto>> SetRoles([FromBody] SetUserRolesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.UserId))
            return BadRequest("UserId is required.");

        var newRoles = (req.Roles ?? Array.Empty<string>())
            .Select(r => r?.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (newRoles.Length == 0)
            return BadRequest("At least one role is required.");

        var invalid = newRoles.Where(r => !AllowedRoles.Contains(r!)).ToArray();
        if (invalid.Length > 0)
            return BadRequest($"Invalid role(s): {string.Join(", ", invalid)}");

        var user = await _users.FindByIdAsync(req.UserId);
        if (user is null) return NotFound("User not found.");

        // Ensure roles exist
        foreach (var role in newRoles)
            if (!await _roles.RoleExistsAsync(role!))
                await _roles.CreateAsync(new IdentityRole(role!));

        // Remove all existing roles, then add requested roles
        var currentRoles = await _users.GetRolesAsync(user);

        var removeResult = await _users.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            return BadRequest(removeResult.Errors);

        var addResult = await _users.AddToRolesAsync(user, newRoles!);
        if (!addResult.Succeeded)
            return BadRequest(addResult.Errors);

        var finalRoles = await _users.GetRolesAsync(user);

        return Ok(new UserWithRolesDto(
            user.Id,
            user.Email,
            user.FullName,
            user.AdminId,
            finalRoles
        ));
    }
}