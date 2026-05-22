using MediatR;

namespace InMoment.Application.Features.Invitations.CancelInvitation;

public sealed record CancelInvitationCommand(Guid InvitationId) : IRequest<Unit>;