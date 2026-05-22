using InMoment.Domain.Friends;

namespace InMoment.Application.Abstractions.Persistence;

public interface IFriendshipRepository
{
    Task AddAsync(Friendship friendship, CancellationToken ct);
    Task<Friendship?> GetByUsersAsync(Guid userAId, Guid userBId, CancellationToken ct);
    Task<IReadOnlyList<Friendship>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    void Remove(Friendship friendship);
}