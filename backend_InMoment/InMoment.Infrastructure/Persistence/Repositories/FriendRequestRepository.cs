using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Friends;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class FriendRequestRepository : IFriendRequestRepository
{
    private readonly AppDbContext _db;

    public FriendRequestRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(FriendRequest request, CancellationToken ct)
        => _db.Set<FriendRequest>().AddAsync(request, ct).AsTask();

    public Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Set<FriendRequest>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<FriendRequest?> GetPendingBetweenUsersAsync(Guid userAId, Guid userBId, CancellationToken ct)
        => _db.Set<FriendRequest>().FirstOrDefaultAsync(x =>
            x.Status == FriendRequestStatus.Pending &&
            ((x.FromUserId == userAId && x.ToUserId == userBId) ||
             (x.FromUserId == userBId && x.ToUserId == userAId)), ct);

    public async Task<IReadOnlyList<FriendRequest>> GetIncomingPendingAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<FriendRequest>()
            .Where(x => x.ToUserId == userId && x.Status == FriendRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FriendRequest>> GetOutgoingPendingAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<FriendRequest>()
            .Where(x => x.FromUserId == userId && x.Status == FriendRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }
}