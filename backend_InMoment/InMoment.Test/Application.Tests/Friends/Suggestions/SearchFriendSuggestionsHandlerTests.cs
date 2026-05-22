using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.Suggestions;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Privacy;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Friends.Suggestions;

public sealed class SearchFriendSuggestionsHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<IFriendRequestRepository> _requests = new();
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SearchFriendSuggestionsHandler Create()
        => new(
            _users.Object,
            _friendships.Object,
            _requests.Object,
            _privacy.Object,
            _blocks.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new SearchFriendSuggestionsQuery("anna"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _users.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenQueryBlank()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        var result = await handler.Handle(
            new SearchFriendSuggestionsQuery("   "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFilterBlockedAndHiddenUsers_AndBuildFlags()
    {
        var currentUserId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();
        var incomingUserId = Guid.NewGuid();
        var outgoingUserId = Guid.NewGuid();
        var hiddenUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var friendUser = User.Create("friend@test.com", "hash", "friend_user", "Friend", "User");
        var incomingUser = User.Create("incoming@test.com", "hash", "incoming_user", "Incoming", "User");
        var outgoingUser = User.Create("outgoing@test.com", "hash", "outgoing_user", "Outgoing", "User");
        var hiddenUser = User.Create("hidden@test.com", "hash", "hidden_user", "Hidden", "User");
        var blockedUser = User.Create("blocked@test.com", "hash", "blocked_user", "Blocked", "User");

        SetEntityId(friendUser, friendUserId);
        SetEntityId(incomingUser, incomingUserId);
        SetEntityId(outgoingUser, outgoingUserId);
        SetEntityId(hiddenUser, hiddenUserId);
        SetEntityId(blockedUser, blockedUserId);

        var hiddenPrivacy = PrivacySettings.CreateDefault(hiddenUserId);
        hiddenPrivacy.Update(
            hiddenPrivacy.AllowFriendRequestsFrom,
            hiddenPrivacy.AllowGroupInvitesFrom,
            hiddenPrivacy.DiscoverableByContacts,
            discoverableBySearch: false);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchAsync("ann", 9, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { friendUser, incomingUser, outgoingUser, hiddenUser, blockedUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, incomingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, outgoingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, hiddenUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _privacy.Setup(x => x.GetByUserIdAsync(friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(incomingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(outgoingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);
        _privacy.Setup(x => x.GetByUserIdAsync(hiddenUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hiddenPrivacy);

        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Friendship.Create(currentUserId, friendUserId));
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, incomingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, outgoingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, incomingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FriendRequest.Create(incomingUserId, currentUserId));
        _requests.Setup(x => x.GetPendingBetweenUsersAsync(currentUserId, outgoingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FriendRequest.Create(currentUserId, outgoingUserId));

        var handler = Create();

        var result = await handler.Handle(
            new SearchFriendSuggestionsQuery("ann", 3),
            CancellationToken.None);

        result.Should().HaveCount(3);

        result[0].UserId.Should().Be(friendUserId);
        result[0].UserName.Should().Be("friend_user");
        result[0].AlreadyFriend.Should().BeTrue();
        result[0].HasIncomingRequest.Should().BeFalse();
        result[0].HasOutgoingRequest.Should().BeFalse();

        result[1].UserId.Should().Be(incomingUserId);
        result[1].AlreadyFriend.Should().BeFalse();
        result[1].HasIncomingRequest.Should().BeTrue();
        result[1].HasOutgoingRequest.Should().BeFalse();

        result[2].UserId.Should().Be(outgoingUserId);
        result[2].AlreadyFriend.Should().BeFalse();
        result[2].HasIncomingRequest.Should().BeFalse();
        result[2].HasOutgoingRequest.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitOutOfRange()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.SearchAsync("alex", 30, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = Create();

        var result = await handler.Handle(
            new SearchFriendSuggestionsQuery("alex", 999),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchAsync("alex", 30, currentUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void SetEntityId(User user, Guid id)
    {
        var property = typeof(InMoment.Domain.Common.Entity<Guid>)
            .GetProperty("Id");

        property!.SetValue(user, id);
    }
}