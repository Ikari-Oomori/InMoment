using InMoment.Application.Features.Contacts.Invites.Common;
using MediatR;

namespace InMoment.Application.Features.Contacts.Invites.List;

public sealed record ListMyContactInvitesQuery()
    : IRequest<IReadOnlyList<ContactInviteDto>>;