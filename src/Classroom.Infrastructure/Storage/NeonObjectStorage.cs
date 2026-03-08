using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Classroom.Infrastructure.FileStorage;

public class NeonObjectStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public NeonObjectStorage(IConfiguration configuration)
    {
        _bucket = configuration["NeonStorage:Bucket"] ?? throw new ArgumentException("NeonStorage:Bucket is not configured");
        var endpoint = configuration["NeonStorage:Endpoint"];
        var accessKey = configuration["NeonStorage:AccessKey"];
        var secretKey = configuration["NeonStorage:SecretKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("NeonStorage:Endpoint/AccessKey/SecretKey must be configured");

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,

            // v3 SDK: prevents STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER
        };

        _s3 = new AmazonS3Client(credentials, config);

        System.Diagnostics.Trace.WriteLine($"NeonObjectStorage config - ServiceURL={config.ServiceURL}, ForcePathStyle={config.ForcePathStyle}");
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
        var effectiveContentType = contentType ?? "application/octet-stream";

        // Write to temp file so the SDK can send with a fixed Content-Length
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{ext}");
        try
        {
            await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(fs, ct);
                await fs.FlushAsync(ct);
            }

            var sizeBytes = new FileInfo(tempPath).Length;

            await using var fileStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = fileStream,
                ContentType = effectiveContentType,
                AutoCloseStream = true
            };

            await _s3.PutObjectAsync(putRequest, ct);
            return (key, sizeBytes, effectiveContentType);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
        }
    }

    public async Task<(Stream stream, string contentType, string downloadFileName)> OpenReadAsync(string storedFileName, string downloadFileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            throw new ArgumentException(nameof(storedFileName));

        var getReq = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = storedFileName
        };

        GetObjectResponse resp;
        try
        {
            resp = await _s3.GetObjectAsync(getReq, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Object '{storedFileName}' not found in bucket '{_bucket}'", storedFileName, ex);
        }

        var contentType = resp.Headers.ContentType ?? "application/octet-stream";
        return (resp.ResponseStream, contentType, downloadFileName);
    }

    public async Task DeleteAsync(string storedFileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storedFileName)) return;
        await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = storedFileName }, ct);
    }

    // Generate a presigned URL using the underlying AmazonS3Client instance.
    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expires)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(nameof(key));

        // _s3 was constructed as AmazonS3Client; cast to access GetPreSignedURL helper.
        if (_s3 is AmazonS3Client client)
        {
            var req = new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expires)
            };

            var url = client.GetPreSignedURL(req);
            return Task.FromResult(url);
        }

        throw new NotSupportedException("Presigned URL generation requires AmazonS3Client instance.");
    }
}