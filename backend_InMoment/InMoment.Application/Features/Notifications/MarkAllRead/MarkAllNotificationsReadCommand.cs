using MediatR;

namespace InMoment.Application.Features.Notifications.MarkAllRead;

public sealed record MarkAllNotificationsReadCommand() : IRequest<Unit>;