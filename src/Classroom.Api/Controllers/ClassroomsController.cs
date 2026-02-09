using Classroom.Application.DTOs;
using Classroom.Domain.Entities;
using Classroom.Domain.Enums;
using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Classroom.Infrastructure.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/classrooms")]
public class ClassroomsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClassroomsController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _userManager.FindByIdAsync(userId);
    }

    // shared seeded categories (fallback)
    private static readonly string[] DefaultCategories = new[] { "Past Papers", "Revision", "Class work" };

    // Existing authenticated endpoint that returns classes for current user
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> MyClasses()
    {
        var appUser = await GetCurrentUserAsync();
        if (appUser is null) return Unauthorized();

        var userId = appUser.Id;

        if (User.IsInRole(AppRole.Teacher))
        {
            // materialize before projecting to avoid jsonb -> text[] SQL cast
            var groups = await _db.ClassroomGroups
                .Where(x => x.TeacherUserId == userId)
                .Include(x => x.Grade)
                .Include(x => x.Subject)
                .AsNoTracking()
                .ToListAsync();

            var classes = groups
                .Select(x => new ClassroomDto(
                    x.Id,
                    x.Name,
                    x.GradeId,
                    x.Grade != null ? x.Grade.Name : string.Empty,
                    x.SubjectId,
                    x.Subject != null ? x.Subject.Name : string.Empty,
                    (x.Categories != null && x.Categories.Any()) ? x.Categories : DefaultCategories
                ))
                .ToList();

            return Ok(classes);
        }

        var enrolled = await _db.Enrollments
            .Where(e => e.LearnerUserId == userId)
            .Select(e => e.ClassroomGroupId)
            .ToListAsync();

        var learnerGroups = await _db.ClassroomGroups
            .Where(x => enrolled.Contains(x.Id))
            .Include(x => x.Grade)
            .Include(x => x.Subject)
            .AsNoTracking()
            .ToListAsync();

        var learnerClasses = learnerGroups
            .Select(x => new ClassroomDto(
                x.Id,
                x.Name,
                x.GradeId,
                x.Grade != null ? x.Grade.Name : string.Empty,
                x.SubjectId,
                x.Subject != null ? x.Subject.Name : string.Empty,
                (x.Categories != null && x.Categories.Any()) ? x.Categories : DefaultCategories
            ))
            .ToList();

        return Ok(learnerClasses);
    }

    // New: public endpoint that returns all classrooms (no role / id checks)
    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllClassrooms(CancellationToken ct = default)
    {
        // materialize whole list then map in memory
        var groups = await _db.ClassroomGroups
            .Include(x => x.Grade)
            .Include(x => x.Subject)
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        var classes = groups
            .Select(x => new ClassroomDto(
                x.Id,
                x.Name,
                x.GradeId,
                x.Grade != null ? x.Grade.Name : string.Empty,
                x.SubjectId,
                x.Subject != null ? x.Subject.Name : string.Empty,
                (x.Categories != null && x.Categories.Any()) ? x.Categories : DefaultCategories
            ))
            .ToList();

        return Ok(classes);
    }

    [HttpPost]
    [Authorize(Roles = AppRole.SuperAdmin + "," + AppRole.Teacher)]
    public async Task<IActionResult> Create([FromBody] CreateClassroomRequest req)
    {
        // Teachers can only create classes for themselves
        if (User.IsInRole(AppRole.Teacher))
        {
            var appUser = await GetCurrentUserAsync();
            if (appUser is null) return Unauthorized();

            if (!string.Equals(appUser.Id, req.TeacherUserId, StringComparison.Ordinal))
                return Forbid();
        }

        var exists = await _db.ClassroomGroups.AnyAsync(c => c.Name == req.Name);
        if (exists) return BadRequest("Class name already exists.");

        var c = new ClassroomGroup
        {
            Name = req.Name.Trim(),
            GradeId = req.GradeId,
            SubjectId = req.SubjectId,
            TeacherUserId = req.TeacherUserId,
            Categories = req.Categories?.ToList() ?? DefaultCategories.ToList()
        };

        _db.ClassroomGroups.Add(c);
        await _db.SaveChangesAsync();

        // return projected DTO to avoid cycles
        var dto = new ClassroomDto(
            c.Id,
            c.Name,
            c.GradeId,
            c.Grade?.Name ?? string.Empty,
            c.SubjectId,
            c.Subject?.Name ?? string.Empty,
            (c.Categories != null && c.Categories.Any()) ? c.Categories : DefaultCategories
        );

        return Ok(dto);
    }

    // Update classroom (PUT) - updates categories too
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRole.SuperAdmin + "," + AppRole.Teacher)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateClassroomRequest req)
    {
        var classroom = await _db.ClassroomGroups.FindAsync(id);
        if (classroom is null) return NotFound("Classroom not found.");

        // Teachers can only update their own class
        if (User.IsInRole(AppRole.Teacher))
        {
            var appUser = await GetCurrentUserAsync();
            if (appUser is null) return Unauthorized();

            if (!string.Equals(appUser.Id, classroom.TeacherUserId, StringComparison.Ordinal))
                return Forbid();
        }

        classroom.Name = req.Name.Trim();
        classroom.GradeId = req.GradeId;
        classroom.SubjectId = req.SubjectId;
        classroom.TeacherUserId = req.TeacherUserId;
        classroom.Categories = req.Categories?.ToList() ?? DefaultCategories.ToList();

        await _db.SaveChangesAsync();

        var dto = new ClassroomDto(
            classroom.Id,
            classroom.Name,
            classroom.GradeId,
            classroom.Grade?.Name ?? string.Empty,
            classroom.SubjectId,
            classroom.Subject?.Name ?? string.Empty,
            (classroom.Categories != null && classroom.Categories.Any()) ? classroom.Categories : DefaultCategories
        );

        return Ok(dto);
    }

    [HttpPost("{classroomId:int}/enroll/{learnerUserId}")]
    [Authorize(Roles = AppRole.SuperAdmin + "," + AppRole.Teacher)]
    public async Task<IActionResult> Enroll(int classroomId, string learnerUserId)
    {
        var classroom = await _db.ClassroomGroups.FirstOrDefaultAsync(x => x.Id == classroomId);
        if (classroom is null) return NotFound("Classroom not found.");

        // Teacher can only enroll for own class
        if (User.IsInRole(AppRole.Teacher))
        {
            var appUser = await GetCurrentUserAsync();
            if (appUser is null) return Unauthorized();

            if (!string.Equals(appUser.Id, classroom.TeacherUserId, StringComparison.Ordinal))
                return Forbid();
        }

        var exists = await _db.Enrollments.AnyAsync(e => e.ClassroomGroupId == classroomId && e.LearnerUserId == learnerUserId);
        if (exists) return Ok("Already enrolled.");

        _db.Enrollments.Add(new Enrollment { ClassroomGroupId = classroomId, LearnerUserId = learnerUserId });
        await _db.SaveChangesAsync();
        return Ok();
    }
}
