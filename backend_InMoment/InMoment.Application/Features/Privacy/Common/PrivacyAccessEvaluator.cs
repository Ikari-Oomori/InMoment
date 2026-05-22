using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Privacy;

namespace InMoment.Application.Features.Privacy.Common;

public sealed class PrivacyAccessEvaluator
{
    private readonly IPrivacySettingsRepository _privacy;
    private readonly IBlockedUserRepository _blocks;
    private readonly IFriendshipRepository _friendships;

    public PrivacyAccessEvaluator(
        IPrivacySettingsRepository privacy,
        IBlockedUserRepository blocks,
        IFriendshipRepository friendships)
    {
        _privacy = privacy;
        _blocks = blocks;
        _friendships = friendships;
    }

    public async Task<bool> IsBlockedEitherWayAsync(Guid currentUserId, Guid targetUserId, CancellationToken ct)
    {
        var blockedByCurrent = await _blocks.ExistsAsync(currentUserId, targetUserId, ct);
        if (blockedByCurrent)
            return true;

        var blockedByTarget = await _blocks.ExistsAsync(targetUserId, currentUserId, ct);
        return blockedByTarget;
    }

    public async Task<bool> CanReceiveFriendRequestAsync(Guid currentUserId, Guid targetUserId, CancellationToken ct)
    {
        if (await IsBlockedEitherWayAsync(currentUserId, targetUserId, ct))
            return false;

        var settings = await _privacy.GetByUserIdAsync(targetUserId, ct);
        if (settings is null)
            return true;

        return settings.AllowFriendRequestsFrom switch
        {
            PrivacyAudience.Everyone => true,
            PrivacyAudience.FriendsOnly => await AreFriendsAsync(currentUserId, targetUserId, ct),
            PrivacyAudience.Nobody => false,
            _ => false
        };
    }

    public async Task<bool> CanReceiveGroupInviteAsync(Guid currentUserId, Guid targetUserId, CancellationToken ct)
    {
        if (await IsBlockedEitherWayAsync(currentUserId, targetUserId, ct))
            return false;

        var settings = await _privacy.GetByUserIdAsync(targetUserId, ct);
        if (settings is null)
            return true;

        return settings.AllowGroupInvitesFrom switch
        {
            PrivacyAudience.Everyone => true,
            PrivacyAudience.FriendsOnly => await AreFriendsAsync(currentUserId, targetUserId, ct),
            PrivacyAudience.Nobody => false,
            _ => false
        };
    }

    public async Task<bool> CanBeDiscoveredByContactsAsync(Guid currentUserId, Guid targetUserId, CancellationToken ct)
    {
        if (await IsBlockedEitherWayAsync(currentUserId, targetUserId, ct))
            return false;

        var settings = await _privacy.GetByUserIdAsync(targetUserId, ct);
        return settings?.DiscoverableByContacts ?? true;
    }

    public async Task<bool> CanBeDiscoveredBySearchAsync(Guid currentUserId, Guid targetUserId, CancellationToken ct)
    {
        if (await IsBlockedEitherWayAsync(currentUserId, targetUserId, ct))
            return false;

        var settings = await _privacy.GetByUserIdAsync(targetUserId, ct);
        return settings?.DiscoverableBySearch ?? true;
    }

    private async Task<bool> AreFriendsAsync(Guid userAId, Guid userBId, CancellationToken ct)
        => await _friendships.GetByUsersAsync(userAId, userBId, ct) is not null;
}