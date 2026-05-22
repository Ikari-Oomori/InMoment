using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using InMoment.Domain.SystemAnnouncements;

namespace InMoment.Application.Features.SystemAnnouncements.Create;

public sealed class CreateSystemAnnouncementHandler
{
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly ISystemAnnouncementRepository _announcements;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRealtime _realtime;
    private readonly INotificationPushDeliveryService _pushDelivery;

    public CreateSystemAnnouncementHandler(
        ISystemModeratorAccess moderatorAccess,
        ICurrentUser currentUser,
        IUserRepository users,
        ISystemAnnouncementRepository announcements,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        INotificationRealtime realtime,
        INotificationPushDeliveryService pushDelivery)
    {
        _moderatorAccess = moderatorAccess;
        _currentUser = currentUser;
        _users = users;
        _announcements = announcements;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _realtime = realtime;
        _pushDelivery = pushDelivery;
    }

    public async Task<Guid> Handle(
        string text,
        string? mediaUrl,
        string? mediaContentType,
        CancellationToken ct)
    {
        if (_currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        _moderatorAccess.EnsureModerator(_currentUser.UserId);

        var announcement = SystemAnnouncement.Create(
            _currentUser.UserId,
            text,
            mediaUrl,
            mediaContentType);

        await _announcements.AddAsync(announcement, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        var userIds = await _users.GetActiveUserIdsAsync(ct);
        var createdNotifications = new List<Notification>(userIds.Count);

        foreach (var userId in userIds)
        {
            var notification = Notification.CreateModeratorAnnouncement(
                userId,
                announcement.Id);

            createdNotifications.Add(notification);
            await _notifications.AddAsync(notification, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var notification in createdNotifications)
        {
            try
            {
                await _pushDelivery.TrySendAsync(notification, ct);

                var unreadCount = await _notifications.GetUnreadCountAsync(
                    notification.UserId,
                    ct);

                await _realtime.NotifyNotificationsChangedAsync(
                    notification.UserId,
                    unreadCount,
                    ct);
            }
            catch
            {
               
            }
        }

        return announcement.Id;
    }
}