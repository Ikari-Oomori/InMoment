using InMoment.Domain.Common;

namespace InMoment.Domain.Privacy;

public sealed class BlockedUser : Entity<Guid>
{
    private BlockedUser() { }

    public Guid UserId { get; private set; }
    public Guid BlockedUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static BlockedUser Create(Guid userId, Guid blockedUserId)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (blockedUserId == Guid.Empty)
            throw new ValidationException("BlockedUserId is required.");

        if (userId == blockedUserId)
            throw new ValidationException("You cannot block yourself.");

        return new BlockedUser
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BlockedUserId = blockedUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}