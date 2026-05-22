namespace InMoment.Application.Abstractions.Realtime;

public interface INotificationRealtime
{
    Task NotifyNotificationsChangedAsync(Guid userId, int unreadCount, CancellationToken ct);
}