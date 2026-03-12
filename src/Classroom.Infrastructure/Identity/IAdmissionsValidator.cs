namespace Classroom.Infrastructure.Identity;

public interface IAdmissionsValidator
{
    Task<bool> IsValidAsync(string adminId, CancellationToken ct = default);
}