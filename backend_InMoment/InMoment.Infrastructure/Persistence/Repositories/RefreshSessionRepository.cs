using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class RefreshSessionRepository : IRefreshSessionRepository
{
    private readonly AppDbContext _db;

    public RefreshSessionRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(RefreshSession session, CancellationToken ct)
        => _db.Set<RefreshSession>().AddAsync(session, ct).AsTask();

    public Task<RefreshSession?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Set<RefreshSession>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<RefreshSession?> GetByTokenHashAsync(string tokenHash, CancellationToken ct)
        => _db.Set<RefreshSession>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshSession>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        return await _db.Set<RefreshSession>()
            .Where(x =>
                x.UserId == userId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > nowUtc)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }
}