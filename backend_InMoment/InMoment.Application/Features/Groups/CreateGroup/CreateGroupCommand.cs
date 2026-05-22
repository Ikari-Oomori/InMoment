using MediatR;

namespace InMoment.Application.Features.Groups.CreateGroup;

public sealed record CreateGroupCommand(string Name) : IRequest<CreateGroupResult>;