using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

csharp src\Classroom.Infrastructure\FileStorage\FileSystemFileStorage.cs
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

    public async Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(Stream content, string originalFileName, string contentType, CancellationToken ct)
        => await SaveFileAsync(content, originalFileName, contentType, ct);

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
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<(Stream, string, string)>((stream, "application/octet-stream", downloadFileName));
    }
}