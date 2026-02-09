using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Classroom.Domain.Entities;

public class ClassroomGroup
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g., "Grade 11 Delphi - Term 1"

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public required string TeacherUserId { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<ResourceFile> Resources { get; set; } = new List<ResourceFile>();

    // Persist categories as JSON (Postgres jsonb). Requires EF migration.
    [Column(TypeName = "jsonb")]
    public List<string> Categories { get; set; } = new();
}
