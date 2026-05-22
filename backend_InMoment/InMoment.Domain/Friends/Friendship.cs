using InMoment.Domain.Common;

namespace InMoment.Domain.Friends;

public sealed class Friendship : Entity<Guid>
{
    private Friendship() { }

    public Guid User1Id { get; private set; }
    public Guid User2Id { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Friendship Create(Guid userAId, Guid userBId)
    {
        if (userAId == Guid.Empty || userBId == Guid.Empty)
            throw new ValidationException("User ids are required.");

        if (userAId == userBId)
            throw new ValidationException("You cannot be friends with yourself.");

        var (user1Id, user2Id) = OrderPair(userAId, userBId);

        return new Friendship
        {
            Id = Guid.NewGuid(),
            User1Id = user1Id,
            User2Id = user2Id,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public bool Involves(Guid userId)
        => User1Id == userId || User2Id == userId;

    public Guid GetOtherUserId(Guid userId)
    {
        if (User1Id == userId) return User2Id;
        if (User2Id == userId) return User1Id;

        throw new ValidationException("User is not part of this friendship.");
    }

    public static (Guid User1Id, Guid User2Id) OrderPair(Guid userAId, Guid userBId)
        => userAId.CompareTo(userBId) < 0
            ? (userAId, userBId)
            : (userBId, userAId);
}
