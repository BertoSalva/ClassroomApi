using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Classroom.Infrastructure.FileStorage;

public interface IFileStorage
{
    Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct);

    // Generic saver for arbitrary file types
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

    Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expires);
}