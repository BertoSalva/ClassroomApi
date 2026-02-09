namespace Classroom.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "ClassroomApi";
    public string Audience { get; set; } = "ClassroomApiClients";
    public string SigningKey { get; set; } = "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_32+";
    public int ExpMinutes { get; set; } = 120;
}
