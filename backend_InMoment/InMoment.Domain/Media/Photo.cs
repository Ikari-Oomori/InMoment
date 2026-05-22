using InMoment.Domain.Common;

namespace InMoment.Domain.Media;

public sealed class Photo : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    public string StorageKey { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string? Caption { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Photo() { }

    public static Photo Create(
        Guid groupId,
        Guid uploadedByUserId,
        string storageKey,
        string contentType,
        long sizeBytes,
        string? caption = null)
    {
        if (groupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        if (uploadedByUserId == Guid.Empty)
            throw new ValidationException("UploadedByUserId is required.");

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ValidationException("StorageKey is required.");

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ValidationException("ContentType is required.");

        if (sizeBytes <= 0)
            throw new ValidationException("SizeBytes must be positive.");

        var normalizedCaption = NormalizeCaption(caption);

        return new Photo
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UploadedByUserId = uploadedByUserId,
            StorageKey = storageKey.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            Caption = normalizedCaption,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public void UpdateCaption(string? caption)
    {
        Caption = NormalizeCaption(caption);
    }

    public void EditCaption(Guid actorUserId, string? caption)
    {
        if (actorUserId != UploadedByUserId)
            throw new ForbiddenException("You are not allowed to edit this photo.");

        Caption = NormalizeCaption(caption);
    }

    public void MarkDeleted(Guid actorUserId, Guid groupOwnerId)
    {
        var canManageGroup = actorUserId == groupOwnerId;
        MarkDeleted(actorUserId, canManageGroup);
    }

    public void MarkDeleted(Guid actorUserId, bool canManageGroup)
    {
        if (actorUserId != UploadedByUserId && !canManageGroup)
            throw new ForbiddenException("You are not allowed to delete this photo.");

        IsDeleted = true;
    }

    public void MarkDeletedByModerator()
    {
        IsDeleted = true;
    }

    private static string? NormalizeCaption(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return null;

        var normalized = caption.Trim();

        if (normalized.Length > 500)
            throw new ValidationException("Caption must be 500 characters or less.");

        return normalized;
    }
}