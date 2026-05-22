using InMoment.Domain.Notifications;

namespace InMoment.Application.Features.Notifications.Common;

public interface INotificationPushDeliveryService
{
    Task TrySendAsync(Notification notification, CancellationToken ct);
}