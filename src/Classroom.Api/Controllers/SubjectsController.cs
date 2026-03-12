using Classroom.Application.DTOs;
using Classroom.Domain.Entities;
using Classroom.Domain.Enums;
using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/subjects")]
public class SubjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubjectsController(AppDbContext db) => _db = db;

    // GET /api/v1/subjects/all
    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<ActionResult<List<SubjectDto>>> GetAll(CancellationToken ct)
    {
        var items = await _db.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.GradeId))
            .ToListAsync(ct);

        return Ok(items);
    }

    // GET /api/v1/subjects?gradeId=1
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<SubjectDto>>> List([FromQuery] int? gradeId, CancellationToken ct)
    {
        var query = _db.Subjects.AsNoTracking();

        if (gradeId is not null)
            query = query.Where(s => s.GradeId == gradeId.Value);

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.GradeId))
            .ToListAsync(ct);

        return Ok(items);
    }

    // GET /api/v1/subjects/123
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<SubjectDto>> GetById(int id, CancellationToken ct)
    {
        var subject = await _db.Subjects
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SubjectDto(s.Id, s.Name, s.GradeId))
            .FirstOrDefaultAsync(ct);

        if (subject is null) return NotFound("Subject not found.");
        return Ok(subject);
    }

    // POST /api/v1/subjects
    [HttpPost]
    [Authorize(Roles = AppRole.SuperAdmin)]
    public async Task<ActionResult<SubjectDto>> Create([FromBody] CreateSubjectRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        var gradeExists = await _db.Grades.AnyAsync(g => g.Id == req.GradeId, ct);
        if (!gradeExists) return BadRequest($"GradeId '{req.GradeId}' does not exist.");

        var entity = new Subject
        {
            Name = req.Name.Trim(),
            GradeId = req.GradeId
        };

        _db.Subjects.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new SubjectDto(entity.Id, entity.Name, entity.GradeId));
    }

    // PUT /api/v1/subjects/123
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRole.SuperAdmin)]
    public async Task<ActionResult<SubjectDto>> Update(int id, [FromBody] UpdateSubjectRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        var entity = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFound("Subject not found.");

        var gradeExists = await _db.Grades.AnyAsync(g => g.Id == req.GradeId, ct);
        if (!gradeExists) return BadRequest($"GradeId '{req.GradeId}' does not exist.");

        entity.Name = req.Name.Trim();
        entity.GradeId = req.GradeId;

        await _db.SaveChangesAsync(ct);

        return Ok(new SubjectDto(entity.Id, entity.Name, entity.GradeId));
    }

    // DELETE /api/v1/subjects/123
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRole.SuperAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFound("Subject not found.");

        // This will fail if a ClassroomGroup references this subject (FK constraint).
        // You can catch and return a nicer error.
        _db.Subjects.Remove(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict("Cannot delete subject because it is referenced by existing classrooms.");
        }

        return NoContent();
    }
}