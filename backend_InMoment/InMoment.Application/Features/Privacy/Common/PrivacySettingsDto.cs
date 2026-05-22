using InMoment.Domain.Privacy;

namespace InMoment.Application.Features.Privacy.Common;

public sealed record PrivacySettingsDto(
    PrivacyAudience AllowFriendRequestsFrom,
    PrivacyAudience AllowGroupInvitesFrom,
    bool DiscoverableByContacts,
    bool DiscoverableBySearch
);