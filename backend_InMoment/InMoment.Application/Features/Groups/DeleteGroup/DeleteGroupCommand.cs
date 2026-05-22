using MediatR;

namespace InMoment.Application.Features.Groups.DeleteGroup;

public sealed record DeleteGroupCommand(Guid GroupId) : IRequest<Unit>;