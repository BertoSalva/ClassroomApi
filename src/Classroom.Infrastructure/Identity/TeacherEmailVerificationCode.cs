using System.ComponentModel.DataAnnotations;

namespace Classroom.Infrastructure.Identity;

public sealed class TeacherEmailVerificationCode
{
    public long Id { get; set; }

    [MaxLength(256)]
    public required string Email { get; set; }

    [MaxLength(16)]
    public required string Code { get; set; }

    public required string ProtectedIdentityToken { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}