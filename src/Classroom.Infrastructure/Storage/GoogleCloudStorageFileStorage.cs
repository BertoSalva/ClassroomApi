using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Classroom.Infrastructure.FileStorage;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace Classroom.Infrastructure.Storage;

public sealed class GoogleCloudStorageFileStorage : IFileStorage
{
    private readonly StorageClient _client;
    private readonly string _bucket;

    public GoogleCloudStorageFileStorage(IConfiguration configuration)
    {
        _bucket = configuration["GoogleCloud:Bucket"]
            ?? throw new ArgumentException("GoogleCloud:Bucket is not configured");

        // Auth uses Application Default Credentials (ADC):
        // - Local dev: gcloud auth application-default login
        // - Server: set GOOGLE_APPLICATION_CREDENTIALS to a service-account json path
        _client = StorageClient.Create();
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
        var effectiveContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        // Ensure start of stream if possible
        if (content.CanSeek) content.Position = 0;

        // Upload
        await _client.UploadObjectAsync(
            bucket: _bucket,
            objectName: key,
            contentType: effectiveContentType,
            source: content,
            options: null,
            cancellationToken: ct);

        // Read metadata so we can return size/content-type
        var obj = await _client.GetObjectAsync(_bucket, key, cancellationToken: ct);
        var sizeBytes = (long)(obj.Size ?? 0);
        var ctValue = string.IsNullOrWhiteSpace(obj.ContentType) ? effectiveContentType : obj.ContentType;

        return (key, sizeBytes, ctValue);
    }

    public async Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(
        string storedFileName,
        string downloadFileName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            throw new ArgumentException(nameof(storedFileName));

        var obj = await _client.GetObjectAsync(_bucket, storedFileName, cancellationToken: ct);

        var ms = new MemoryStream();
        await _client.DownloadObjectAsync(_bucket, storedFileName, ms, cancellationToken: ct);
        ms.Position = 0;

        var contentType = string.IsNullOrWhiteSpace(obj.ContentType) ? "application/octet-stream" : obj.ContentType;
        return (ms, contentType, downloadFileName);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct)
        => string.IsNullOrWhiteSpace(storedFileName)
            ? Task.CompletedTask
            : _client.DeleteObjectAsync(_bucket, storedFileName, cancellationToken: ct);

    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expires)
        => throw new NotSupportedException("GCS signed URLs require URL signing (not implemented).");
}