using Classroom.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Classroom.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly LocalFileStorageOptions _options;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(_options.RootPath);
    }

    public async Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct)
    {
        if (!string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only PDF files are allowed.");

        // Generate safe stored filename
        var ext = Path.GetExtension(originalFileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            ext = ".pdf";

        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_options.RootPath, storedFileName);

        long total = 0;
        const int bufSize = 81920;
        var buffer = new byte[bufSize];

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufSize, useAsync: true);
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            total += read;
            if (total > _options.MaxPdfBytes)
            {
                fs.Close();
                File.Delete(fullPath);
                throw new InvalidOperationException($"PDF too large. Max allowed is {_options.MaxPdfBytes} bytes.");
            }

            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return (storedFileName, total, "application/pdf");
    }

    public Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(
        string storedFileName,
        string downloadFileName,
        CancellationToken ct)
    {
        var fullPath = Path.Combine(_options.RootPath, storedFileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.");

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
        return Task.FromResult((stream, "application/pdf", downloadFileName));
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct)
    {
        var fullPath = Path.Combine(_options.RootPath, storedFileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
