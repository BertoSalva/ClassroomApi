using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Classroom.Infrastructure.FileStorage;

public interface IFileStorage
{
    // Generic file save for arbitrary types
    Task<(string storedFileName, long sizeBytes, string contentType)> SaveFileAsync(
         Stream content,
         string originalFileName,
         string contentType,
         CancellationToken ct);

    Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(
        string storedFileName,
        string downloadFileName,
        CancellationToken ct);

    Task DeleteAsync(string storedFileName, CancellationToken ct);
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<Classroom.Infrastructure.FileStorage.IFileStorage, Classroom.Infrastructure.FileStorage.FileSystemFileStorage>();

        return services;
    }
}       