using MediatR;

namespace InMoment.Application.Features.Groups.InviteCodes.Join;

public sealed record JoinByCodeCommand(string Code) : IRequest;