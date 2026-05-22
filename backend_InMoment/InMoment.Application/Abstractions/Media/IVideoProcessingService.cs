namespace InMoment.Application.Abstractions.Media;

public interface IVideoProcessingService
{
    Task<VideoProcessingResult> TrimAndNormalizeAsync(
        VideoProcessingRequest request,
        CancellationToken ct);
}

public sealed record VideoProcessingRequest(
    string SourceStorageKey,
    string TargetStorageKey,
    long TrimStartMs,
    long TrimEndMs
);

public sealed record VideoProcessingResult(
    string StorageKey,
    string ContentType,
    long SizeBytes
);