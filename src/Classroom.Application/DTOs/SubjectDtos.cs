namespace Classroom.Application.DTOs;

public sealed record SubjectDto(
    int Id,
    string Name,
    int GradeId
);

public sealed record CreateSubjectRequest(
    string Name,
    int GradeId
);

public sealed record UpdateSubjectRequest(
    string Name,
    int GradeId
);