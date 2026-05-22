using InMoment.Domain.Common;

namespace InMoment.Domain.Friends;

public sealed class FriendRequest : Entity<Guid>
{
    private FriendRequest() { }

    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public FriendRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    public static FriendRequest Create(Guid fromUserId, Guid toUserId)
    {
        if (fromUserId == Guid.Empty)
            throw new ValidationException("FromUserId is required.");

        if (toUserId == Guid.Empty)
            throw new ValidationException("ToUserId is required.");

        if (fromUserId == toUserId)
            throw new ValidationException("You cannot send a friend request to yourself.");

        return new FriendRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Accept()
    {
        EnsurePending();
        Status = FriendRequestStatus.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
    }

    public void Reject()
    {
        EnsurePending();
        Status = FriendRequestStatus.Rejected;
        RespondedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        EnsurePending();
        Status = FriendRequestStatus.Cancelled;
        RespondedAtUtc = DateTime.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != FriendRequestStatus.Pending)
            throw new ValidationException("Only a pending friend request can be changed.");
    }
}
