using MediatR;

namespace InMoment.Application.Features.Invitations.AcceptInvitation;

public sealed record AcceptInvitationCommand(Guid InvitationId)
    : IRequest<Unit>;