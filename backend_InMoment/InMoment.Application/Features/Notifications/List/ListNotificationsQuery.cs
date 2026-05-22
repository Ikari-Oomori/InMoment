using MediatR;

namespace InMoment.Application.Features.Notifications.List;

public sealed record ListNotificationsQuery(
    int Limit = 20,
    string? Cursor = null
) : IRequest<NotificationsPageDto>;