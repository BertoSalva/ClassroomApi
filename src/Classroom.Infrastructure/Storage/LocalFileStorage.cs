using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Classroom.Infrastructure.FileStorage;
using Microsoft.Extensions.Options;

namespace Classroom.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly LocalFileStorageOptions _options;
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _rootPath = _options.RootPath ?? throw new ArgumentException("RootPath must be configured", nameof(options));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct)
    {
        if (!string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only PDF files are allowed.");

        var ext = Path.GetExtension(originalFileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            ext = ".pdf";

        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_rootPath, storedFileName);

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

    public async Task<(string storedFileName, long sizeBytes, string contentType)> SaveFileAsync(Stream content, string originalFileName, string contentType, CancellationToken ct)
    {
        var ext = Path.GetExtension(originalFileName) ?? string.Empty;
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(_rootPath, storedFileName);

        await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
        await fs.FlushAsync(ct);

        var sizeBytes = new FileInfo(path).Length;
        return (storedFileName, sizeBytes, contentType ?? "application/octet-stream");
    }

    public Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(
        string storedFileName,
        string downloadFileName,
        CancellationToken ct)
    {
        var fullPath = Path.Combine(_rootPath, storedFileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", storedFileName);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);

        var ext = Path.GetExtension(storedFileName);
        var contentType = string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";

        return Task.FromResult((stream, contentType, downloadFileName));
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct)
    {
        var fullPath = Path.Combine(_rootPath, storedFileName);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    // Local testing: return file:// URI. Not for production.
    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expires)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(nameof(key));
        var fullPath = Path.Combine(_rootPath, key);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", key);

        var uri = new Uri(Path.GetFullPath(fullPath));
        return Task.FromResult(uri.AbsoluteUri);
    }
}
