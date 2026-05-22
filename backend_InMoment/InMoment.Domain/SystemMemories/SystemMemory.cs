using InMoment.Domain.Common;

namespace InMoment.Domain.SystemMemories;

public sealed class SystemMemory : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public SystemMemoryPeriod Period { get; private set; }
    public string Title { get; private set; } = default!;
    public string Subtitle { get; private set; } = default!;
    public string SourcePhotoIds { get; private set; } = default!;
    public Guid? PreviewPhotoId { get; private set; }
    public string? GeneratedVideoStorageKey { get; private set; }
    public string? GeneratedVideoContentType { get; private set; }
    public long? GeneratedVideoSizeBytes { get; private set; }
    public DateTime PeriodStartedAtUtc { get; private set; }
    public DateTime PeriodEndedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ViewedAtUtc { get; private set; }

    private SystemMemory() { }

    public static SystemMemory Create(
        Guid userId,
        SystemMemoryPeriod period,
        IReadOnlyList<Guid> sourcePhotoIds,
        Guid? previewPhotoId,
        DateTime periodStartedAtUtc,
        DateTime periodEndedAtUtc,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (sourcePhotoIds.Count == 0)
            throw new ValidationException("System memory requires at least one source photo.");

        var months = (int)period;

        return new SystemMemory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Period = period,
            Title = $"Ваши {months} мес. в InMoment",
            Subtitle = $"Мы собрали моменты за последние {months} мес.",
            SourcePhotoIds = string.Join(';', sourcePhotoIds.Select(x => x.ToString("D"))),
            PreviewPhotoId = previewPhotoId,
            PeriodStartedAtUtc = periodStartedAtUtc,
            PeriodEndedAtUtc = periodEndedAtUtc,
            CreatedAtUtc = nowUtc
        };
    }

    public IReadOnlyList<Guid> GetSourcePhotoIds()
    {
        if (string.IsNullOrWhiteSpace(SourcePhotoIds))
            return Array.Empty<Guid>();

        return SourcePhotoIds
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToList();
    }

    public void AttachGeneratedVideo(string storageKey, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ValidationException("StorageKey is required.");

        GeneratedVideoStorageKey = storageKey.Trim();
        GeneratedVideoContentType = string.IsNullOrWhiteSpace(contentType) ? "video/mp4" : contentType.Trim();
        GeneratedVideoSizeBytes = sizeBytes > 0 ? sizeBytes : null;
    }

    public void MarkViewed(DateTime nowUtc)
    {
        ViewedAtUtc ??= nowUtc;
    }
}
