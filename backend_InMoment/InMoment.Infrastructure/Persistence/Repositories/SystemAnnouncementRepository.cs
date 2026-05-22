using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.SystemAnnouncements;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class SystemAnnouncementRepository : ISystemAnnouncementRepository
{
    private readonly AppDbContext _db;

    public SystemAnnouncementRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(SystemAnnouncement announcement, CancellationToken ct)
        => _db.SystemAnnouncements.AddAsync(announcement, ct).AsTask();

    public Task<SystemAnnouncement?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.SystemAnnouncements.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<SystemAnnouncement>> GetLatestAsync(int limit, CancellationToken ct)
    {
        return await _db.SystemAnnouncements
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, SystemAnnouncement>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, SystemAnnouncement>();

        return await _db.SystemAnnouncements
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    public void Remove(SystemAnnouncement announcement)
    {
        _db.SystemAnnouncements.Remove(announcement);
    }
}