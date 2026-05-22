using InMoment.Domain.Common;

namespace InMoment.Domain.Media;

public sealed class Reaction : Entity<Guid>
{
    public Guid PhotoId { get; private set; }
    public Guid UserId { get; private set; }
    public ReactionType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Reaction() { }

    public static Reaction Create(Guid photoId, Guid userId, ReactionType type)
    {
        if (photoId == Guid.Empty) throw new ValidationException("PhotoId is required.");
        if (userId == Guid.Empty) throw new ValidationException("UserId is required.");
        if (type == ReactionType.None) throw new ValidationException("ReactionType is required.");

        var now = DateTime.UtcNow;

        return new Reaction
        {
            Id = Guid.NewGuid(),
            PhotoId = photoId,
            UserId = userId,
            Type = type,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Change(ReactionType type)
    {
        if (type == ReactionType.None) throw new ValidationException("ReactionType is required.");
        Type = type;
        UpdatedAt = DateTime.UtcNow;
    }
}