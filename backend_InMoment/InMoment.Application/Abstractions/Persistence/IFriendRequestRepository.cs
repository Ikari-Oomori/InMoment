using InMoment.Domain.Friends;

namespace InMoment.Application.Abstractions.Persistence;

public interface IFriendRequestRepository
{
    Task AddAsync(FriendRequest request, CancellationToken ct);
    Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<FriendRequest?> GetPendingBetweenUsersAsync(Guid userAId, Guid userBId, CancellationToken ct);
    Task<IReadOnlyList<FriendRequest>> GetIncomingPendingAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<FriendRequest>> GetOutgoingPendingAsync(Guid userId, CancellationToken ct);
}
