using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Notifications.MarkRead;

public sealed class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadCommand, Unit>
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public MarkNotificationReadHandler(
        INotificationRepository notifications,
        INotificationRealtime notificationRealtime,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var item = await _notifications.GetByIdAsync(cmd.NotificationId, ct)
                   ?? throw new NotFoundException("Notification not found.");

        if (item.UserId != _current.UserId)
            throw new ForbiddenException("You cannot modify this notification.");

        item.MarkRead();
        await _uow.SaveChangesAsync(ct);

        var unreadCount = await _notifications.GetUnreadCountAsync(_current.UserId, ct);
        await _notificationRealtime.NotifyNotificationsChangedAsync(_current.UserId, unreadCount, ct);

        return Unit.Value;
    }
}