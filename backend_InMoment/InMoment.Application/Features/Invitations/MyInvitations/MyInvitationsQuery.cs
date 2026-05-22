using MediatR;

namespace InMoment.Application.Features.Invitations.MyInvitations;

public sealed record MyInvitationsQuery() : IRequest<IReadOnlyList<InvitationDto>>;