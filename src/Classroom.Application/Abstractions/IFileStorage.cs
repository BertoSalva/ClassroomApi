namespace Classroom.Application.Abstractions;

public interface IFileStorage
{
    Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(
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
