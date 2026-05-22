using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Friends;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class FriendshipRepository : IFriendshipRepository
{
    private readonly AppDbContext _db;

    public FriendshipRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Friendship friendship, CancellationToken ct)
        => _db.Set<Friendship>().AddAsync(friendship, ct).AsTask();

    public Task<Friendship?> GetByUsersAsync(Guid userAId, Guid userBId, CancellationToken ct)
    {
        var (user1Id, user2Id) = Friendship.OrderPair(userAId, userBId);

        return _db.Set<Friendship>().FirstOrDefaultAsync(
            x => x.User1Id == user1Id && x.User2Id == user2Id, ct);
    }

    public async Task<IReadOnlyList<Friendship>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<Friendship>()
            .Where(x => x.User1Id == userId || x.User2Id == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public void Remove(Friendship friendship)
        => _db.Set<Friendship>().Remove(friendship);
}