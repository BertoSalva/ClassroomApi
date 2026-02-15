namespace Classroom.Domain.Entities;

public class ResourceFile
{
    public int Id { get; set; }
    public int ClassroomGroupId { get; set; }
    public ClassroomGroup? ClassroomGroup { get; set; }
    public string Category { get; set; }
    public required string Title { get; set; }
    public required string StoredFileName { get; set; } // internal name on disk/storage
    public required string OriginalFileName { get; set; } // what teacher uploaded
    public required string ContentType { get; set; } // application/pdf
    public long SizeBytes { get; set; }

    public required string UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
