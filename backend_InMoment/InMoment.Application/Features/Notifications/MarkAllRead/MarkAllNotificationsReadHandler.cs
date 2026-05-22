using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Notifications.MarkAllRead;

public sealed class MarkAllNotificationsReadHandler : IRequestHandler<MarkAllNotificationsReadCommand, Unit>
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public MarkAllNotificationsReadHandler(
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

    public async Task<Unit> Handle(MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        var unread = await _notifications.GetUnreadByUserAsync(_current.UserId, ct);

        foreach (var item in unread)
            item.MarkRead();

        await _uow.SaveChangesAsync(ct);

        await _notificationRealtime.NotifyNotificationsChangedAsync(_current.UserId, 0, ct);

        return Unit.Value;
    }
}