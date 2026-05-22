using InMoment.Domain.Common;

namespace InMoment.Domain.Media;

public sealed class CommentReaction : Entity<Guid>
{
    public Guid CommentId { get; private set; }
    public Guid UserId { get; private set; }
    public ReactionType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CommentReaction() { }

    public static CommentReaction Create(Guid commentId, Guid userId, ReactionType type)
    {
        if (commentId == Guid.Empty)
            throw new ValidationException("CommentId is required.");

        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (type == ReactionType.None)
            throw new ValidationException("ReactionType is required.");

        var now = DateTime.UtcNow;

        return new CommentReaction
        {
            Id = Guid.NewGuid(),
            CommentId = commentId,
            UserId = userId,
            Type = type,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Change(ReactionType type)
    {
        if (type == ReactionType.None)
            throw new ValidationException("ReactionType is required.");

        Type = type;
        UpdatedAt = DateTime.UtcNow;
    }
}