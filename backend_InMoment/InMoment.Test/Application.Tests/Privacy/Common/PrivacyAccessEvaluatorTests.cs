using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Features.Privacy.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Privacy;
using Moq;

namespace InMoment.Tests.Application.Tests.Privacy.Common;

public sealed class PrivacyAccessEvaluatorTests
{
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IFriendshipRepository> _friendships = new();

    private PrivacyAccessEvaluator Create()
        => new(_privacy.Object, _blocks.Object, _friendships.Object);

    [Fact]
    public async Task IsBlockedEitherWayAsync_ShouldReturnTrue_WhenBlockedByCurrentUser()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = Create();

        var result = await sut.IsBlockedEitherWayAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();

        _blocks.Verify(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()), Times.Once);
        _blocks.Verify(x => x.ExistsAsync(targetUserId, currentUserId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsBlockedEitherWayAsync_ShouldReturnTrue_WhenBlockedByTargetUser()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsAsync(targetUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = Create();

        var result = await sut.IsBlockedEitherWayAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsBlockedEitherWayAsync_ShouldReturnFalse_WhenNoBlocksExist()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsAsync(targetUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = Create();

        var result = await sut.IsBlockedEitherWayAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanReceiveFriendRequestAsync_ShouldReturnFalse_WhenBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = Create();

        var result = await sut.CanReceiveFriendRequestAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();

        _privacy.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanReceiveFriendRequestAsync_ShouldReturnTrue_WhenSettingsMissing()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        var sut = Create();

        var result = await sut.CanReceiveFriendRequestAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanReceiveFriendRequestAsync_ShouldReturnTrue_WhenAudienceEveryone()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            PrivacyAudience.Everyone,
            settings.AllowGroupInvitesFrom,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = Create();

        var result = await sut.CanReceiveFriendRequestAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
        _friendships.Verify(x => x.GetByUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanReceiveFriendRequestAsync_ShouldReturnTrue_WhenAudienceFriendsOnly_AndUsersAreFriends()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            PrivacyAudience.FriendsOnly,
            settings.AllowGroupInvitesFrom,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Friendship.Create(currentUserId, targetUserId));

        var sut = Create();

        var result = await sut.CanReceiveFriendRequestAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanReceiveFriendRequestAsync_ShouldReturnFalse_WhenAudienceFriendsOnly_AndUsersAreNotFriends()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            PrivacyAudience.FriendsOnly,
            settings.AllowGroupInvitesFrom,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        var sut = Create();

        var result = await sut.CanReceiveFriendRequestAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanReceiveFriendRequestAsync_ShouldReturnFalse_WhenAudienceNobody()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            PrivacyAudience.Nobody,
            settings.AllowGroupInvitesFrom,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = Create();

        var result = await sut.CanReceiveFriendRequestAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanReceiveGroupInviteAsync_ShouldReturnFalse_WhenBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = Create();

        var result = await sut.CanReceiveGroupInviteAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanReceiveGroupInviteAsync_ShouldReturnTrue_WhenSettingsMissing()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        var sut = Create();

        var result = await sut.CanReceiveGroupInviteAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanReceiveGroupInviteAsync_ShouldReturnTrue_WhenAudienceEveryone()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            settings.AllowFriendRequestsFrom,
            PrivacyAudience.Everyone,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = Create();

        var result = await sut.CanReceiveGroupInviteAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanReceiveGroupInviteAsync_ShouldReturnTrue_WhenAudienceFriendsOnly_AndUsersAreFriends()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            settings.AllowFriendRequestsFrom,
            PrivacyAudience.FriendsOnly,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Friendship.Create(currentUserId, targetUserId));

        var sut = Create();

        var result = await sut.CanReceiveGroupInviteAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanReceiveGroupInviteAsync_ShouldReturnFalse_WhenAudienceFriendsOnly_AndUsersAreNotFriends()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            settings.AllowFriendRequestsFrom,
            PrivacyAudience.FriendsOnly,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        var sut = Create();

        var result = await sut.CanReceiveGroupInviteAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanReceiveGroupInviteAsync_ShouldReturnFalse_WhenAudienceNobody()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            settings.AllowFriendRequestsFrom,
            PrivacyAudience.Nobody,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = Create();

        var result = await sut.CanReceiveGroupInviteAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanBeDiscoveredByContactsAsync_ShouldReturnFalse_WhenBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = Create();

        var result = await sut.CanBeDiscoveredByContactsAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanBeDiscoveredByContactsAsync_ShouldReturnTrue_WhenSettingsMissing()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        var sut = Create();

        var result = await sut.CanBeDiscoveredByContactsAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanBeDiscoveredByContactsAsync_ShouldReturnConfiguredValue()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            settings.AllowFriendRequestsFrom,
            settings.AllowGroupInvitesFrom,
            discoverableByContacts: false,
            settings.DiscoverableBySearch);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = Create();

        var result = await sut.CanBeDiscoveredByContactsAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanBeDiscoveredBySearchAsync_ShouldReturnFalse_WhenBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = Create();

        var result = await sut.CanBeDiscoveredBySearchAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanBeDiscoveredBySearchAsync_ShouldReturnTrue_WhenSettingsMissing()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        var sut = Create();

        var result = await sut.CanBeDiscoveredBySearchAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanBeDiscoveredBySearchAsync_ShouldReturnConfiguredValue()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        SetupNotBlocked(currentUserId, targetUserId);

        var settings = PrivacySettings.CreateDefault(targetUserId);
        settings.Update(
            settings.AllowFriendRequestsFrom,
            settings.AllowGroupInvitesFrom,
            settings.DiscoverableByContacts,
            discoverableBySearch: false);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var sut = Create();

        var result = await sut.CanBeDiscoveredBySearchAsync(currentUserId, targetUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    private void SetupNotBlocked(Guid currentUserId, Guid targetUserId)
    {
        _blocks.Setup(x => x.ExistsAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsAsync(targetUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }
}