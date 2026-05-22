using InMoment.Domain.Common;

namespace InMoment.Domain.Privacy;

public sealed class PrivacySettings : Entity<Guid>
{
    private PrivacySettings() { }

    public Guid UserId { get; private set; }

    public PrivacyAudience AllowFriendRequestsFrom { get; private set; }
    public PrivacyAudience AllowGroupInvitesFrom { get; private set; }
    public bool DiscoverableByContacts { get; private set; }
    public bool DiscoverableBySearch { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static PrivacySettings CreateDefault(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var now = DateTime.UtcNow;

        return new PrivacySettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AllowFriendRequestsFrom = PrivacyAudience.Everyone,
            AllowGroupInvitesFrom = PrivacyAudience.Everyone,
            DiscoverableByContacts = true,
            DiscoverableBySearch = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(
        PrivacyAudience allowFriendRequestsFrom,
        PrivacyAudience allowGroupInvitesFrom,
        bool discoverableByContacts,
        bool discoverableBySearch)
    {
        AllowFriendRequestsFrom = allowFriendRequestsFrom;
        AllowGroupInvitesFrom = allowGroupInvitesFrom;
        DiscoverableByContacts = discoverableByContacts;
        DiscoverableBySearch = discoverableBySearch;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}