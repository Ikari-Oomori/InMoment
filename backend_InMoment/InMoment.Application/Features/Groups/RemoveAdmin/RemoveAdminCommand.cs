using MediatR;

namespace InMoment.Application.Features.Groups.RemoveAdmin;

public sealed record RemoveAdminCommand(Guid GroupId, Guid UserId) : IRequest<Unit>;