namespace Classroom.Application.DTOs;

public sealed record ClassroomDto(
    int Id,
    string Name,
    int GradeId,
    string GradeName,
    int SubjectId,
    string SubjectName,
    IEnumerable<string> Categories // new
);