namespace InMoment.Application.Abstractions.Storage;

public interface IFileStorage
{
    Task<PresignedUploadResult> GetPresignedUploadUrlAsync(
        PresignedUploadRequest request,
        CancellationToken ct);

    string GetPublicUrl(string key);

    Task DownloadToFileAsync(
        string key,
        string destinationPath,
        CancellationToken ct);

    Task<long> UploadFileAsync(
        string key,
        string sourcePath,
        string contentType,
        CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed record PresignedUploadRequest(
    string Key,
    string ContentType,
    TimeSpan? ExpiresIn = null
);

public sealed record PresignedUploadResult(
    string UploadUrl,
    string Key,
    string FileUrl,
    DateTimeOffset ExpiresAt
);