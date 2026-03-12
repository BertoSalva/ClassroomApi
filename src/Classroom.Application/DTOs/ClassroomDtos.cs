namespace Classroom.Application.DTOs;

public sealed record ClassroomDto(
    int Id,
    int GradeId,
    string GradeName,
    int SubjectId,
    string SubjectName,
    IEnumerable<string> Categories // new
);