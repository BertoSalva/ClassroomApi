namespace Classroom.Domain.Entities;

public class Grade
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g., "Grade 11"
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}
