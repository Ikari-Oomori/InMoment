using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Privacy.UpdatePrivacy;

public sealed record UpdatePrivacyCommand(
    PrivacyAudience AllowFriendRequestsFrom,
    PrivacyAudience AllowGroupInvitesFrom,
    bool DiscoverableByContacts,
    bool DiscoverableBySearch
) : IRequest;