namespace Classroom.Domain.Entities;

public class Subject
{
    public int Id { get; set; }
    public required string Name { get; set; } // e.g., "Maths", "Delphi"
    public int GradeId { get; set; }
    public Grade? Grade { get; set; }
}
