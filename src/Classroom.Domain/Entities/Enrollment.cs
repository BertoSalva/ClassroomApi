namespace Classroom.Domain.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int ClassroomGroupId { get; set; }
    public ClassroomGroup? ClassroomGroup { get; set; }

    public required string LearnerUserId { get; set; }
    public DateTimeOffset EnrolledAt { get; set; } = DateTimeOffset.UtcNow;
}
