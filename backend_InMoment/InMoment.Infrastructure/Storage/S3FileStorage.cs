using Amazon.S3;
using Amazon.S3.Model;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Storage;

public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly StorageOptions _options;

    public S3FileStorage(
        IAmazonS3 s3,
        IOptions<StorageOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<PresignedUploadResult> GetPresignedUploadUrlAsync(
        PresignedUploadRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ValidationException("Key is required.");

        if (string.IsNullOrWhiteSpace(request.ContentType))
            throw new ValidationException("ContentType is required.");

        var key = NormalizeKey(request.Key);

        var expiresIn = request.ExpiresIn ?? TimeSpan.FromMinutes(_options.PresignExpiryMinutes);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);

        var presignRequest = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = request.ContentType.Trim()
        };

        var url = _s3.GetPreSignedURL(presignRequest);
        url = NormalizePresignedScheme(url);

        return new PresignedUploadResult(
            url,
            key,
            GetPublicUrl(key),
            expiresAt);
    }

    public string GetPublicUrl(string key)
    {
        var normalizedKey = NormalizeKey(key);

        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            throw new ValidationException("Storage public base url is required.");

        return $"{_options.PublicBaseUrl.Trim().TrimEnd('/')}/{normalizedKey}";
    }

    public async Task DownloadToFileAsync(
        string key,
        string destinationPath,
        CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.Bucket,
            Key = normalizedKey
        }, ct);

        await using var file = File.Create(destinationPath);
        await response.ResponseStream.CopyToAsync(file, ct);
    }

    public async Task<long> UploadFileAsync(
        string key,
        string sourcePath,
        string contentType,
        CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new ValidationException("Source file does not exist.");

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ValidationException("ContentType is required.");

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = normalizedKey,
            FilePath = sourcePath,
            ContentType = contentType.Trim()
        };

        await _s3.PutObjectAsync(putRequest, ct);

        return new FileInfo(sourcePath).Length;
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var normalizedKey = key.Trim().TrimStart('/');

        if (normalizedKey.Length == 0)
            return;

        await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.Bucket,
            Key = normalizedKey
        }, ct);
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ValidationException("Key is required.");

        var normalizedKey = key.Trim().TrimStart('/');

        if (normalizedKey.Length == 0)
            throw new ValidationException("Key is required.");

        return normalizedKey;
    }

    private string NormalizePresignedScheme(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var presignedUri))
            return url;

        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpointUri))
            return url;

        if (!string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return url;

        if (string.Equals(presignedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(presignedUri.Host, endpointUri.Host, StringComparison.OrdinalIgnoreCase) &&
            presignedUri.Port == endpointUri.Port)
        {
            var builder = new UriBuilder(presignedUri)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = endpointUri.Port
            };

            return builder.Uri.ToString();
        }

        return url;
    }
}