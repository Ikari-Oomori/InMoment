using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Notifications.GetUnreadCount;

public sealed class GetUnreadNotificationsCountHandler
    : IRequestHandler<GetUnreadNotificationsCountQuery, UnreadNotificationsCountDto>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUser _current;

    public GetUnreadNotificationsCountHandler(
        INotificationRepository notifications,
        ICurrentUser current)
    {
        _notifications = notifications;
        _current = current;
    }

    public async Task<UnreadNotificationsCountDto> Handle(GetUnreadNotificationsCountQuery q, CancellationToken ct)
    {
        var count = await _notifications.GetUnreadCountAsync(_current.UserId, ct);
        return new UnreadNotificationsCountDto(count);
    }
}