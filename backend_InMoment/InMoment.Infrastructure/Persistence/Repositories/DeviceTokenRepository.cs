using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly AppDbContext _db;

    public DeviceTokenRepository(AppDbContext db) => _db = db;

    public Task AddAsync(DeviceToken token, CancellationToken ct)
        => _db.Set<DeviceToken>().AddAsync(token, ct).AsTask();

    public Task<DeviceToken?> GetByIdAsync(Guid deviceTokenId, CancellationToken ct)
        => _db.Set<DeviceToken>().FirstOrDefaultAsync(x => x.Id == deviceTokenId, ct);

    public Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken ct)
        => _db.Set<DeviceToken>().FirstOrDefaultAsync(x => x.Token == token, ct);

    public async Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<DeviceToken>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastUsedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DeviceToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<DeviceToken>()
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.LastUsedAtUtc)
            .ToListAsync(ct);
    }
}