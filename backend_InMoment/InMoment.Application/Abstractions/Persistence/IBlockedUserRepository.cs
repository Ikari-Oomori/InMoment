using InMoment.Domain.Privacy;

namespace InMoment.Application.Abstractions.Persistence;

public interface IBlockedUserRepository
{
    Task AddAsync(BlockedUser blockedUser, CancellationToken ct);
    Task<BlockedUser?> GetAsync(Guid userId, Guid blockedUserId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid userId, Guid blockedUserId, CancellationToken ct);
    Task<bool> ExistsEitherDirectionAsync(Guid userAId, Guid userBId, CancellationToken ct);
    Task<IReadOnlyList<BlockedUser>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    void Remove(BlockedUser blockedUser);
}