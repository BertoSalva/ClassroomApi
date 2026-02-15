using Classroom.Application.Abstractions;
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

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/resources")]
public class ResourcesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly UserManager<ApplicationUser> _userManager;

    public ResourcesController(AppDbContext db, IFileStorage storage, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _storage = storage;
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

    [HttpPost("{classroomId:int}/upload")]
    [Authorize(Roles = AppRole.SuperAdmin + "," + AppRole.Teacher)]
    [RequestSizeLimit(30_000_000)]
    public async Task<ActionResult<UploadResourceResponse>> Upload(
        int classroomId,
        [FromForm] string title,
        [FromForm] string category,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only application/pdf is allowed.");

        var classroom = await _db.ClassroomGroups.FirstOrDefaultAsync(x => x.Id == classroomId, ct);
        if (classroom is null)
            return NotFound("Classroom not found.");

        var appUser = await GetCurrentUserAsync();
        if (appUser is null)
            return Unauthorized();

        var userId = appUser.Id;

        await using var stream = file.OpenReadStream();
        var (stored, sizeBytes, contentType) = await _storage.SavePdfAsync(stream, file.FileName, file.ContentType, ct);

        var res = new ResourceFile
        {
            ClassroomGroupId = classroomId,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Past Papers" : category.Trim(),
            StoredFileName = stored,
            OriginalFileName = file.FileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedByUserId = userId,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _db.ResourceFiles.Add(res);
        await _db.SaveChangesAsync(ct);

        return Ok(new UploadResourceResponse(res.Id, res.Title, res.OriginalFileName, res.SizeBytes, res.UploadedAt));
    }

    [HttpGet("{resourceId:int}/download")]
    [Authorize]
    public async Task<IActionResult> Download(int resourceId, CancellationToken ct)
    {
        var resource = await _db.ResourceFiles
            .Include(r => r.ClassroomGroup)
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct);

        if (resource is null)
            return NotFound("Resource not found.");

        var appUser = await GetCurrentUserAsync();
        if (appUser is null)
            return Unauthorized();

        var userId = appUser.Id;

        // Access control:
        // - SuperAdmin can download anything
        // - Teacher can download resources of their class
        // - Learner can download if enrolled
        if (User.IsInRole(AppRole.Teacher))
        {
            if (resource.ClassroomGroup is null || resource.ClassroomGroup.TeacherUserId != userId)
                return Forbid();
        }
        else if (User.IsInRole(AppRole.Learner))
        {
            var enrolled = await _db.Enrollments.AnyAsync(
                e => e.ClassroomGroupId == resource.ClassroomGroupId && e.LearnerUserId == userId, ct);
            if (!enrolled)
                return Forbid();
        }

        var (stream, downloadContentType, downloadName) = await _storage.OpenReadAsync(
            resource.StoredFileName,
            resource.OriginalFileName,
            ct);
        return File(stream, downloadContentType, downloadName, enableRangeProcessing: true);
    }

    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllResources(CancellationToken ct)
    {
        var resources = await _db.ResourceFiles
            .Include(r => r.ClassroomGroup)
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Category,
                r.OriginalFileName,
                r.SizeBytes,
                r.ContentType,
                r.UploadedAt,
                ClassroomId = r.ClassroomGroupId,
                ClassroomName = r.ClassroomGroup != null ? r.ClassroomGroup.Name : string.Empty
            })
            .ToListAsync(ct);

        return Ok(resources);
    }

    [HttpGet("{classroomId:int}")]
    [Authorize]
    public async Task<IActionResult> ListForClass(int classroomId, CancellationToken ct)
    {
        var resources = await _db.ResourceFiles
            .Where(r => r.ClassroomGroupId == classroomId)
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Category,
                r.OriginalFileName,
                r.SizeBytes,
                r.UploadedAt
            })
            .ToListAsync(ct);

        return Ok(resources);
    }
}