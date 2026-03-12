namespace Classroom.Application.DTOs;

public sealed record SetUserRolesRequest(
    string UserId,
    IEnumerable<string> Roles
);

public sealed record UserWithRolesDto(
    string Id,
    string? Email,
    string? FullName,
    string? AdminId,
    IEnumerable<string> Roles
);