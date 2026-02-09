namespace Classroom.Application.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role, // SuperAdmin / Teacher / Learner
    string? AdminId = null // optional admin identifier for learners
);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);