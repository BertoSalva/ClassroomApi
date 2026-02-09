using Microsoft.AspNetCore.Http;

namespace Classroom.Application.DTOs;

public sealed record CreateClassroomRequest(
    string Name,
    int GradeId,
    int SubjectId,
    string TeacherUserId,
    IEnumerable<string>? Categories = null
);

public sealed record UploadResourceResponse(
    int ResourceId,
    string Title,
    string OriginalFileName,
    long SizeBytes,
    DateTimeOffset UploadedAt
);

public sealed class UploadResourceRequest
{
    // optional title; if empty controller will default to filename
    public string? Title { get; set; }

    // the file picker in Swagger will be shown for this property
    public IFormFile? File { get; set; }
}
