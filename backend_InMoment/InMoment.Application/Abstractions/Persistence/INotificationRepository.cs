using InMoment.Domain.Notifications;

namespace InMoment.Application.Abstractions.Persistence;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct);

    Task<Notification?> GetByIdAsync(Guid notificationId, CancellationToken ct);

    Task<IReadOnlyList<Notification>> GetPageByUserAsync(
        Guid userId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforeNotificationId,
        CancellationToken ct);

    Task<IReadOnlyList<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken ct);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct);

    Task<int> GetUnreadReactionCountForPhotoAsync(
        Guid userId,
        Guid groupId,
        Guid photoId,
        CancellationToken ct);

    Task<Notification?> FindLatestUnreadCollapsibleAsync(
        Guid userId,
        NotificationType type,
        Guid? actorUserId,
        Guid? groupId,
        Guid? photoId,
        CancellationToken ct);

    Task<IReadOnlyList<Notification>> GetBySystemAnnouncementIdAsync(
        Guid systemAnnouncementId,
        CancellationToken ct);

    void RemoveRange(IEnumerable<Notification> notifications);
}