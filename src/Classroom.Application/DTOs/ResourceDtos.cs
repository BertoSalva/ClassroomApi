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
