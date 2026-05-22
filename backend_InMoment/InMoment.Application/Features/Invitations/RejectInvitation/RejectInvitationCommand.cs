using MediatR;

namespace InMoment.Application.Features.Invitations.RejectInvitation;

public sealed record RejectInvitationCommand(Guid InvitationId) : IRequest<Unit>;