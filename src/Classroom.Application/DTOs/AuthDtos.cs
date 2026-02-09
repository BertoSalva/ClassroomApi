namespace Classroom.Application.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role, // SuperAdmin / Teacher / Learner
    string? AdminId = null
);

public sealed record LoginRequest(string? Email, string Password, string? AdminId = null);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);
