using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Classroom.Infrastructure.Identity;

public interface ITeacherEmailVerificationStore
{
    string Issue(string email, string identityToken, TimeSpan ttl);
    bool TryConsume(string email, string code, out string identityToken);
}

public sealed class TeacherEmailVerificationStore : ITeacherEmailVerificationStore
{
    private const string Purpose = "TeacherEmailVerificationCode_v1";
    private readonly IMemoryCache _cache;
    private readonly IDataProtector _protector;

    public TeacherEmailVerificationStore(IMemoryCache cache, IDataProtectionProvider dataProtectionProvider)
    {
        _cache = cache;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Issue(string email, string identityToken, TimeSpan ttl)
    {
        var code = GenerateCode();
        var protectedToken = _protector.Protect(identityToken);

        _cache.Set(CacheKey(email, code), protectedToken, ttl);
        return code;
    }

    public bool TryConsume(string email, string code, out string identityToken)
    {
        identityToken = string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return false;

        if (!_cache.TryGetValue(CacheKey(email, code), out string? protectedToken) || string.IsNullOrWhiteSpace(protectedToken))
            return false;

        _cache.Remove(CacheKey(email, code));

        identityToken = _protector.Unprotect(protectedToken);
        return true;
    }

    private static string CacheKey(string email, string code) => $"teacher-email-verify:{email.Trim().ToLowerInvariant()}:{code.Trim()}";

    private static string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var value = BitConverter.ToUInt32(bytes) % 1_000_000;
        return value.ToString("D6");
    }
}