using Classroom.Domain.Enums;
using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db) => _db = db;

    [HttpPost("grades")]
    public async Task<IActionResult> CreateGrade([FromBody] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name required.");
        _db.Grades.Add(new Classroom.Domain.Entities.Grade { Name = name.Trim() });
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject([FromQuery] int gradeId, [FromBody] string name)
    {
        var grade = await _db.Grades.FirstOrDefaultAsync(x => x.Id == gradeId);
        if (grade is null) return NotFound("Grade not found.");
        _db.Subjects.Add(new Classroom.Domain.Entities.Subject { GradeId = gradeId, Name = name.Trim() });
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades()
    {
        var grades = await _db.Grades.Include(g => g.Subjects).ToListAsync();
        return Ok(grades);
    }
}
