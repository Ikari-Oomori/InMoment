using InMoment.Domain.Common;

namespace InMoment.Domain.Media;

public sealed class SavedPhoto : Entity<Guid>
{
    public Guid PhotoId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SavedPhoto() { }

    public static SavedPhoto Create(Guid photoId, Guid userId)
    {
        if (photoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        return new SavedPhoto
        {
            Id = Guid.NewGuid(),
            PhotoId = photoId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }
}