using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Classroom.Infrastructure.FileStorage;
using Microsoft.Extensions.Configuration;

namespace Classroom.Infrastructure.Storage;

public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(IConfiguration configuration)
    {
        var conn = configuration["AzureBlob:ConnectionString"]
            ?? throw new ArgumentException("AzureBlob:ConnectionString is not configured");

        var containerName = configuration["AzureBlob:Container"]
            ?? throw new ArgumentException("AzureBlob:Container is not configured");

        _container = new BlobContainerClient(conn, containerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public Task<(string storedFileName, long sizeBytes, string contentType)> SavePdfAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct)
        => SaveFileAsync(content, originalFileName, contentType, ct);

    public async Task<(string storedFileName, long sizeBytes, string contentType)> SaveFileAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct)
    {
        var ext = Path.GetExtension(originalFileName ?? string.Empty);
        var key = $"{Guid.NewGuid():N}{ext}";
        var ctValue = contentType ?? "application/octet-stream";

        var blob = _container.GetBlobClient(key);

        var headers = new BlobHttpHeaders { ContentType = ctValue };

        content.Position = content.CanSeek ? 0 : content.Position;

        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);

        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        var sizeBytes = props.Value.ContentLength;

        return (key, sizeBytes, ctValue);
    }

    public async Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(
        string storedFileName,
        string downloadFileName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            throw new ArgumentException(nameof(storedFileName));

        var blob = _container.GetBlobClient(storedFileName);

        var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
        var contentType = download.Value.Details.ContentType ?? "application/octet-stream";

        return (download.Value.Content, contentType, downloadFileName);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct)
        => string.IsNullOrWhiteSpace(storedFileName)
            ? Task.CompletedTask
            : _container.DeleteBlobIfExistsAsync(storedFileName, cancellationToken: ct);

    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expires)
        => throw new NotSupportedException("Use Azure SAS if you need presigned URLs (not implemented here).");
}