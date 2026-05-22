using MediatR;

namespace InMoment.Application.Features.Notifications.GetUnreadCount;

public sealed record GetUnreadNotificationsCountQuery() : IRequest<UnreadNotificationsCountDto>;

public sealed record UnreadNotificationsCountDto(int Count);