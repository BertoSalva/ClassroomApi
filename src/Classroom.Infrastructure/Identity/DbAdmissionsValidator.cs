using Classroom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Classroom.Infrastructure.Identity;

public sealed class DbAdmissionsValidator : IAdmissionsValidator
{
    private readonly AppDbContext _db;

    public DbAdmissionsValidator(AppDbContext db) => _db = db;

    public Task<bool> IsValidAsync(string adminId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminId)) return Task.FromResult(false);

        var needle = Normalize(adminId);
        if (needle.Length == 0) return Task.FromResult(false);

        return _db.AdmissionsNumbers
            .AsNoTracking()
            .AnyAsync(x => x.AdminId == needle, ct);
    }

    private static string Normalize(string value)
        => new string(value.Where(char.IsDigit).ToArray());
}