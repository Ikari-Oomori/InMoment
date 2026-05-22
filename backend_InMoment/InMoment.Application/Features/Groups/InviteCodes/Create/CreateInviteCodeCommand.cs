using MediatR;

namespace InMoment.Application.Features.Groups.InviteCodes.Create;

public sealed record CreateInviteCodeCommand(
    Guid GroupId,
    int? MaxUses,
    int? ExpireHours
) : IRequest<string>;