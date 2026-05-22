using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class NotificationSettingsRepository : INotificationSettingsRepository
{
    private readonly AppDbContext _db;

    public NotificationSettingsRepository(AppDbContext db) => _db = db;

    public Task<NotificationSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct)
        => _db.Set<NotificationSettings>()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public Task AddAsync(NotificationSettings settings, CancellationToken ct)
        => _db.Set<NotificationSettings>().AddAsync(settings, ct).AsTask();
}