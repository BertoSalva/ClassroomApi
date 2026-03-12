using Classroom.Application.Abstractions;
using Classroom.Application.DTOs;
using Classroom.Domain.Entities;
using Classroom.Domain.Enums;
using Classroom.Infrastructure.FileStorage;
using Classroom.Infrastructure.Identity;
using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http;

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
    [RequestSizeLimit(30_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadResourceResponse>> Upload(int classroomId, [FromForm] UploadResourceRequest req, CancellationToken ct)
    {
        var file = req?.File;
        var title = req?.Title ?? string.Empty;
        var category = req?.Category ?? string.Empty;

        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        // Allowed mime types/extensions
        var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/zip",
            "application/x-zip-compressed",
            "application/octet-stream", // some clients
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "image/png",
            "image/jpeg"
        };

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".zip", ".doc", ".docx", ".png", ".jpg", ".jpeg"
        };

        var ext = Path.GetExtension(file.FileName ?? string.Empty);
        if (!allowedExtensions.Contains(ext) || !allowedMimeTypes.Contains(file.ContentType ?? ""))
            return BadRequest("File type not allowed. Allowed: pdf, zip, doc, docx, png, jpg.");

        var classroom = await _db.ClassroomGroups.FirstOrDefaultAsync(x => x.Id == classroomId, ct);
        if (classroom is null)
            return NotFound("Classroom not found.");

        


        await using var stream = file.OpenReadStream();
        // Generic save for arbitrary file types
        var (stored, sizeBytes, contentType) = await _storage.SaveFileAsync(stream, file.FileName, file.ContentType ?? "application/octet-stream", ct);

        var res = new ResourceFile
        {
            ClassroomGroupId = classroomId,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Past Papers" : category.Trim(),
            StoredFileName = stored,
            OriginalFileName = file.FileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ResourceYear = req.ResourceYear,
            Term = req.Term,
            UploadedByUserId = "",
            UploadedAt = DateTimeOffset.UtcNow
        };

        _db.ResourceFiles.Add(res);
        await _db.SaveChangesAsync(ct);

        return Ok(new UploadResourceResponse(
            res.Id,
            res.Title,
            res.OriginalFileName,
            res.SizeBytes,
            res.UploadedAt,
            res.ResourceYear,
            res.Term
));
    }

    [HttpGet("{resourceId:int}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(int resourceId, CancellationToken ct)
    {
        var resource = await _db.ResourceFiles
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct);

        if (resource is null)
            return NotFound(new { message = "Resource not found." });

        if (string.IsNullOrWhiteSpace(resource.StoredFileName))
            return BadRequest(new { message = "File reference is missing. Resource may be corrupted." });

        try
        {
            var (stream, downloadContentType, downloadName) = await _storage.OpenReadAsync(
                resource.StoredFileName,
                resource.OriginalFileName,
                ct);

            return File(stream, downloadContentType, downloadName, enableRangeProcessing: true);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = $"File not found in storage: {resource.StoredFileName}", error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error downloading file", error = ex.Message, storedFileName = resource.StoredFileName });
        }
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
                r.ResourceYear,
                r.Term,
                ClassroomId = r.ClassroomGroupId,
                StoredFileName = r.StoredFileName
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
                r.UploadedAt,
                r.ResourceYear,
                r.Term
            })
            .ToListAsync(ct);

        return Ok(resources);
    }

    // DELETE /api/v1/resources/123
    [HttpDelete("{resourceId:int}")]
    [Authorize(Roles = AppRole.SuperAdmin + "," + AppRole.Teacher)]
    public async Task<IActionResult> Delete(int resourceId, CancellationToken ct)
    {
        var res = await _db.ResourceFiles.FirstOrDefaultAsync(r => r.Id == resourceId, ct);
        if (res is null) return NotFound("Resource not found.");

        // Optional: if Teacher, ensure they own the classroom (matches your other patterns)
        if (User.IsInRole(AppRole.Teacher))
        {
            var classroom = await _db.ClassroomGroups.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == res.ClassroomGroupId, ct);

            if (classroom is null) return NotFound("Classroom not found.");

            var appUser = await GetCurrentUserAsync();
            if (appUser is null) return Unauthorized();

            if (!string.Equals(appUser.Id, classroom.TeacherUserId, StringComparison.Ordinal))
                return Forbid();
        }

        _db.ResourceFiles.Remove(res);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}

public class UploadResourceRequest
{
    [FromForm] public string Title { get; set; } = string.Empty;
    [FromForm] public string Category { get; set; } = string.Empty;
    [FromForm] public DateTimeOffset? ResourceYear { get; set; }
    [FromForm] public int? Term { get; set; }
    [FromForm] public IFormFile File { get; set; } = default!;
}