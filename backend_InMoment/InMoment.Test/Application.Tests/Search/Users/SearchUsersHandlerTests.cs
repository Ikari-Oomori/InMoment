using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Search.Users;
using InMoment.Domain.Privacy;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Search.Users;

public sealed class SearchUsersHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SearchUsersHandler Create()
        => new(_users.Object, _privacy.Object, _blocks.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenQueryBlank()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        var result = await handler.Handle(
            new SearchUsersQuery("   "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitOutOfRange()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchAsync("anna", 10, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = Create();

        var result = await handler.Handle(
            new SearchUsersQuery("anna", 999),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchAsync("anna", 10, currentUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFilterBlockedHiddenAndInactiveUsers_AndMapDtos()
    {
        var currentUserId = Guid.NewGuid();
        var visibleUserId = Guid.NewGuid();
        var hiddenUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();

        var visibleUser = User.Create("visible@test.com", "hash", "visible_user", "Anna", "Ivanova");
        var hiddenUser = User.Create("hidden@test.com", "hash", "hidden_user", "Hidden", "User");
        var blockedUser = User.Create("blocked@test.com", "hash", "blocked_user", "Blocked", "User");
        var inactiveUser = User.Create("inactive@test.com", "hash", "inactive_user", "Inactive", "User");

        SetEntityId(visibleUser, visibleUserId);
        SetEntityId(hiddenUser, hiddenUserId);
        SetEntityId(blockedUser, blockedUserId);
        SetEntityId(inactiveUser, inactiveUserId);

        visibleUser.SetProfilePhoto("https://cdn.example.com/profiles/visible.jpg");
        inactiveUser.Deactivate();

        var hiddenPrivacy = PrivacySettings.CreateDefault(hiddenUserId);
        hiddenPrivacy.Update(
            hiddenPrivacy.AllowFriendRequestsFrom,
            hiddenPrivacy.AllowGroupInvitesFrom,
            hiddenPrivacy.DiscoverableByContacts,
            discoverableBySearch: false);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchAsync("ann", 10, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visibleUser, hiddenUser, blockedUser, inactiveUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visibleUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, hiddenUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _privacy.Setup(x => x.GetByUserIdAsync(visibleUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(hiddenUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hiddenPrivacy);

        var handler = Create();

        var result = await handler.Handle(
            new SearchUsersQuery("ann"),
            CancellationToken.None);

        result.Should().HaveCount(1);

        result[0].Id.Should().Be(visibleUserId);
        result[0].UserName.Should().Be("visible_user");
        result[0].DisplayName.Should().Be("Anna Ivanova");
        result[0].ProfilePhotoUrl.Should().Be("https://cdn.example.com/profiles/visible.jpg");
    }

    [Fact]
    public async Task Handle_ShouldFallbackToUserName_WhenFullNameEmpty()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var user = User.Create("user@test.com", "hash", "nickname_only", "Temp", "User");
        SetEntityId(user, targetUserId);
        SetStringProperty(user, nameof(User.FirstName), "");
        SetStringProperty(user, nameof(User.LastName), "");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchAsync("nick", 10, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privacy.Setup(x => x.GetByUserIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        var handler = Create();

        var result = await handler.Handle(
            new SearchUsersQuery("nick"),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].DisplayName.Should().Be("nickname_only");
    }

    private static void SetEntityId(User user, Guid id)
    {
        var property = typeof(InMoment.Domain.Common.Entity<Guid>).GetProperty("Id");
        property!.SetValue(user, id);
    }

    private static void SetStringProperty(User user, string propertyName, string value)
    {
        var property = typeof(User).GetProperty(propertyName);
        property!.SetValue(user, value);
    }
}