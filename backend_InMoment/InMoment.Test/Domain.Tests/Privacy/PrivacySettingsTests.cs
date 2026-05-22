using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;

namespace InMoment.Domain.Tests.Privacy;

public sealed class PrivacySettingsTests
{
    [Fact]
    public void CreateDefault_ShouldThrowValidationException_WhenUserIdEmpty()
    {
        var act = () => PrivacySettings.CreateDefault(Guid.Empty);

        act.Should().Throw<ValidationException>()
            .WithMessage("UserId is required.");
    }

    [Fact]
    public void CreateDefault_ShouldCreateDefaultSettings_WhenValid()
    {
        var userId = Guid.NewGuid();

        var result = PrivacySettings.CreateDefault(userId);

        result.Id.Should().NotBe(Guid.Empty);
        result.UserId.Should().Be(userId);
        result.AllowFriendRequestsFrom.Should().Be(PrivacyAudience.Everyone);
        result.AllowGroupInvitesFrom.Should().Be(PrivacyAudience.Everyone);
        result.DiscoverableByContacts.Should().BeTrue();
        result.DiscoverableBySearch.Should().BeTrue();
        result.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_ShouldChangeAllFields_AndRefreshUpdatedAtUtc()
    {
        var userId = Guid.NewGuid();
        var settings = PrivacySettings.CreateDefault(userId);
        var oldUpdatedAt = settings.UpdatedAtUtc;

        Thread.Sleep(10);

        settings.Update(
            PrivacyAudience.FriendsOnly,
            PrivacyAudience.Nobody,
            discoverableByContacts: false,
            discoverableBySearch: false);

        settings.AllowFriendRequestsFrom.Should().Be(PrivacyAudience.FriendsOnly);
        settings.AllowGroupInvitesFrom.Should().Be(PrivacyAudience.Nobody);
        settings.DiscoverableByContacts.Should().BeFalse();
        settings.DiscoverableBySearch.Should().BeFalse();
        settings.UpdatedAtUtc.Should().BeAfter(oldUpdatedAt);
    }

    [Theory]
    [InlineData(PrivacyAudience.Everyone, PrivacyAudience.Everyone, true, true)]
    [InlineData(PrivacyAudience.FriendsOnly, PrivacyAudience.FriendsOnly, false, true)]
    [InlineData(PrivacyAudience.Nobody, PrivacyAudience.Nobody, false, false)]
    public void Update_ShouldSupportAllPrivacyAudienceCombinations(
        PrivacyAudience allowFriendRequestsFrom,
        PrivacyAudience allowGroupInvitesFrom,
        bool discoverableByContacts,
        bool discoverableBySearch)
    {
        var settings = PrivacySettings.CreateDefault(Guid.NewGuid());

        settings.Update(
            allowFriendRequestsFrom,
            allowGroupInvitesFrom,
            discoverableByContacts,
            discoverableBySearch);

        settings.AllowFriendRequestsFrom.Should().Be(allowFriendRequestsFrom);
        settings.AllowGroupInvitesFrom.Should().Be(allowGroupInvitesFrom);
        settings.DiscoverableByContacts.Should().Be(discoverableByContacts);
        settings.DiscoverableBySearch.Should().Be(discoverableBySearch);
    }
}