using MediatR;

namespace InMoment.Application.Features.Notifications.MarkRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Unit>;