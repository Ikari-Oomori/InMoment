using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Privacy;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class BlockedUserRepository : IBlockedUserRepository
{
    private readonly AppDbContext _db;

    public BlockedUserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(BlockedUser blockedUser, CancellationToken ct)
        => _db.Set<BlockedUser>().AddAsync(blockedUser, ct).AsTask();

    public Task<BlockedUser?> GetAsync(Guid userId, Guid blockedUserId, CancellationToken ct)
        => _db.Set<BlockedUser>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.BlockedUserId == blockedUserId, ct);

    public Task<bool> ExistsAsync(Guid userId, Guid blockedUserId, CancellationToken ct)
        => _db.Set<BlockedUser>()
            .AnyAsync(x => x.UserId == userId && x.BlockedUserId == blockedUserId, ct);

    public Task<bool> ExistsEitherDirectionAsync(Guid userAId, Guid userBId, CancellationToken ct)
        => _db.Set<BlockedUser>()
            .AnyAsync(x =>
                (x.UserId == userAId && x.BlockedUserId == userBId) ||
                (x.UserId == userBId && x.BlockedUserId == userAId), ct);

    public async Task<IReadOnlyList<BlockedUser>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<BlockedUser>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public void Remove(BlockedUser blockedUser)
        => _db.Set<BlockedUser>().Remove(blockedUser);
}