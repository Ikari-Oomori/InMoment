using InMoment.Domain.Privacy;

namespace InMoment.API.Modules.Privacy;

public sealed record UpdatePrivacyRequest(
    PrivacyAudience AllowFriendRequestsFrom,
    PrivacyAudience AllowGroupInvitesFrom,
    bool DiscoverableByContacts,
    bool DiscoverableBySearch
);