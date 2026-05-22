using InMoment.Domain.Common;

namespace InMoment.Domain.Media;

public sealed class Comment : Entity<Guid>
{
    public Guid PhotoId { get; private set; }
    public Guid UserId { get; private set; }

    public Guid? ParentCommentId { get; private set; }

    public string Text { get; private set; } = string.Empty;
    public string? GifUrl { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Comment() { }

    public static Comment CreateRoot(Guid photoId, Guid userId, string? text, string? gifUrl = null)
        => CreateInternal(photoId, userId, parentCommentId: null, text, gifUrl);

    public static Comment CreateReply(Guid photoId, Guid userId, Guid parentCommentId, string? text, string? gifUrl = null)
    {
        if (parentCommentId == Guid.Empty)
            throw new ValidationException("ParentCommentId is required.");

        return CreateInternal(photoId, userId, parentCommentId, text, gifUrl);
    }

    private static Comment CreateInternal(
        Guid photoId,
        Guid userId,
        Guid? parentCommentId,
        string? text,
        string? gifUrl)
    {
        if (photoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var normalizedText = NormalizeText(text, allowEmpty: true);
        var normalizedGifUrl = NormalizeGifUrl(gifUrl);

        if (string.IsNullOrWhiteSpace(normalizedText) && string.IsNullOrWhiteSpace(normalizedGifUrl))
            throw new ValidationException("Comment text must be 1..500 characters.");
        
        return new Comment
        {
            Id = Guid.NewGuid(),
            PhotoId = photoId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            Text = normalizedText,
            GifUrl = normalizedGifUrl,
            CreatedAt = DateTime.UtcNow,
            EditedAt = null,
            IsDeleted = false
        };
    }

    public void Edit(Guid actorUserId, string text)
    {
        if (IsDeleted)
            throw new ValidationException("Deleted comment cannot be edited.");

        if (actorUserId != UserId)
            throw new ForbiddenException("You are not allowed to edit this comment.");

        var normalized = NormalizeText(text, allowEmpty: false);

        if (string.Equals(Text, normalized, StringComparison.Ordinal))
            return;

        Text = normalized;
        EditedAt = DateTime.UtcNow;
    }

    public void Delete(Guid actorUserId)
    {
        if (actorUserId != UserId)
            throw new ForbiddenException("You are not allowed to delete this comment.");

        IsDeleted = true;
    }

    public void DeleteAsOwner(Guid actorUserId)
    {
        IsDeleted = true;
    }

    private static string NormalizeText(string? text, bool allowEmpty)
    {
        var normalized = (text ?? string.Empty).Trim();

        if (normalized.Length > 500)
            throw new ValidationException("Comment text must be 0..500 characters.");

        if (!allowEmpty && normalized.Length < 1)
            throw new ValidationException("Comment text must be 1..500 characters.");

        return normalized;
    }

    private static string? NormalizeGifUrl(string? gifUrl)
    {
        var normalized = (gifUrl ?? string.Empty).Trim();

        if (normalized.Length == 0)
            return null;

        if (normalized.Length > 2048)
            throw new ValidationException("GIF URL is too long.");

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            throw new ValidationException("Invalid GIF URL.");

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new ValidationException("GIF URL must be http or https.");

        return normalized;
    }
}