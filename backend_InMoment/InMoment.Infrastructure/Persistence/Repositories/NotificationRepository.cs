using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db) => _db = db;

    public Task AddAsync(Notification notification, CancellationToken ct)
        => _db.Set<Notification>().AddAsync(notification, ct).AsTask();

    public Task<Notification?> GetByIdAsync(Guid notificationId, CancellationToken ct)
        => _db.Set<Notification>().FirstOrDefaultAsync(x => x.Id == notificationId, ct);

    public async Task<IReadOnlyList<Notification>> GetPageByUserAsync(
        Guid userId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforeNotificationId,
        CancellationToken ct)
    {
        var query = _db.Set<Notification>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CreatedAt >= DateTime.UtcNow.AddDays(-14));

        if (beforeCreatedAt.HasValue && beforeNotificationId.HasValue)
        {
            var dt = beforeCreatedAt.Value;
            var id = beforeNotificationId.Value;

            query = query.Where(x =>
                x.CreatedAt < dt ||
                (x.CreatedAt == dt && x.Id.CompareTo(id) < 0));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Set<Notification>()
            .Where(x => x.UserId == userId && !x.IsRead && x.CreatedAt >= DateTime.UtcNow.AddDays(-14))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct)
        => _db.Set<Notification>()
            .CountAsync(x => x.UserId == userId && !x.IsRead && x.CreatedAt >= DateTime.UtcNow.AddDays(-14), ct);

    public Task<int> GetUnreadReactionCountForPhotoAsync(
        Guid userId,
        Guid groupId,
        Guid photoId,
        CancellationToken ct)
    {
        return _db.Set<Notification>()
            .CountAsync(x =>
                x.UserId == userId &&
                !x.IsRead &&
                x.Type == NotificationType.ReactionOnPhoto &&
                x.GroupId == groupId &&
                x.PhotoId == photoId,
                ct);
    }

    public Task<Notification?> FindLatestUnreadCollapsibleAsync(
        Guid userId,
        NotificationType type,
        Guid? actorUserId,
        Guid? groupId,
        Guid? photoId,
        CancellationToken ct)
    {
        return _db.Set<Notification>()
            .Where(x =>
                x.UserId == userId &&
                !x.IsRead &&
                x.Type == type &&
                x.ActorUserId == actorUserId &&
                x.GroupId == groupId &&
                x.PhotoId == photoId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetBySystemAnnouncementIdAsync(
        Guid systemAnnouncementId,
        CancellationToken ct)
    {
        return await _db.Notifications
            .Where(x => x.SystemAnnouncementId == systemAnnouncementId)
            .ToListAsync(ct);
    }

    public void RemoveRange(IEnumerable<Notification> notifications)
    {
        _db.Notifications.RemoveRange(notifications);
    }
}