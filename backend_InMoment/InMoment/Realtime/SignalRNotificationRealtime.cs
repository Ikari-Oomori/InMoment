using InMoment.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace InMoment.API.Realtime;

public sealed class SignalRNotificationRealtime : INotificationRealtime
{
    private readonly IHubContext<UsersHub, IUsersClient> _hub;

    public SignalRNotificationRealtime(IHubContext<UsersHub, IUsersClient> hub)
        => _hub = hub;

    public Task NotifyNotificationsChangedAsync(Guid userId, int unreadCount, CancellationToken ct)
    {
        return _hub.Clients
            .Group(UsersHub.UserGroupName(userId))
            .NotificationsChanged(unreadCount);
    }
}