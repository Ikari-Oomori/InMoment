using MediatR;

namespace InMoment.Application.Features.Users.SetActiveGroup;

public sealed record SetActiveGroupCommand(Guid GroupId) : IRequest<Unit>;