using MediatR;

namespace InMoment.Application.Features.Groups.LeaveGroup;

public sealed record LeaveGroupCommand(Guid GroupId) : IRequest<Unit>;