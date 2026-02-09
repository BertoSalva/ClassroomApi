csharp src\Classroom.Api\Controllers\LookupController.cs
using Microsoft.AspNetCore.Mvc;
using Classroom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Classroom.Api.Controllers;

[ApiController]
[Route("api/v1/lookups")]
public class LookupController : ControllerBase
{
    private readonly AppDbContext _db;
    public LookupController(AppDbContext db) => _db = db;

    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades()
    {
        var grades = await _db.Grades.OrderBy(g => g.Id)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();
        return Ok(grades);
    }

    [HttpGet("grades/{gradeId:int}/subjects")]
    public async Task<IActionResult> GetSubjects(int gradeId)
    {
        var subjects = await _db.Subjects
            .Where(s => s.GradeId == gradeId)
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();
        return Ok(subjects);
    }
}