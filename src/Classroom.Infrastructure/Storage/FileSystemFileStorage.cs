using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Classroom.Infrastructure.FileStorage;

public class FileSystemFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public FileSystemFileStorage(IConfiguration config)
    {
        var configured = config["Storage:RootPath"];
        _rootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads")
            : configured;

        Directory.CreateDirectory(_rootPath);
    }

    public Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(Stream content, string originalFileName, string contentType, CancellationToken ct)
        => SaveFileAsync(content, originalFileName, contentType, ct);

    public async Task<(string storedFileName, long sizeBytes, string contentType)> SaveFileAsync(Stream content, string originalFileName, string contentType, CancellationToken ct)
    {
        var ext = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(_rootPath, storedFileName);

        await using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
        await fs.FlushAsync(ct);

        var sizeBytes = new FileInfo(path).Length;
        return (storedFileName, sizeBytes, contentType ?? "application/octet-stream");
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct)
    {
        var path = Path.Combine(_rootPath, storedFileName);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(string storedFileName, string downloadFileName, CancellationToken ct)
    {
        var path = Path.Combine(_rootPath, storedFileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Stored file not found at path '{path}'", path);

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        // Basic content-type inference by extension (fallback to octet-stream)
        var ext = Path.GetExtension(downloadFileName ?? string.Empty).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

        return Task.FromResult<(Stream, string, string)>((stream, contentType, downloadFileName));
    }

    // Local testing: return file:// URI. Not for production.
    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expires)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(nameof(key));
        var path = Path.Combine(_rootPath, key);
        if (!File.Exists(path))
            throw new FileNotFoundException("Stored file not found.", key);

        var uri = new Uri(Path.GetFullPath(path));
        return Task.FromResult(uri.AbsoluteUri);
    }
}