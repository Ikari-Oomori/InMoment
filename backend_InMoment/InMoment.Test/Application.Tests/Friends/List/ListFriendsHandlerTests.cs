using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.List;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Friends.List;

public sealed class ListFriendsHandlerTests
{
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();

    private ListFriendsHandler Create()
        => new(_friendships.Object, _users.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new ListFriendsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _friendships.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoFriendships()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _friendships.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Friendship>());

        var handler = Create();

        var result = await handler.Handle(new ListFriendsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSkipMissingUsers_AndSortByFirstNameThenLastName()
    {
        var currentUserId = Guid.NewGuid();
        var friendAId = Guid.NewGuid();
        var friendBId = Guid.NewGuid();
        var missingFriendId = Guid.NewGuid();

        var friendshipA = Friendship.Create(currentUserId, friendAId);
        var friendshipB = Friendship.Create(currentUserId, friendBId);
        var friendshipMissing = Friendship.Create(currentUserId, missingFriendId);

        SetCreatedAtUtc(friendshipA, new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(friendshipB, new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(friendshipMissing, new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        var userB = User.Create("b@test.com", "hash", "beta_user", "Beta", "User");
        userB.SetProfilePhoto("https://cdn.example.com/profiles/beta.jpg");
        SetEntityId(userB, friendBId);

        var userA = User.Create("a@test.com", "hash", "alpha_user", "Alpha", "User");
        SetEntityId(userA, friendAId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _friendships.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { friendshipB, friendshipMissing, friendshipA });

        _users.Setup(x => x.GetByIdAsync(friendAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userA);

        _users.Setup(x => x.GetByIdAsync(friendBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userB);

        _users.Setup(x => x.GetByIdAsync(missingFriendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var result = await handler.Handle(new ListFriendsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].UserId.Should().Be(friendAId);
        result[0].UserName.Should().Be("alpha_user");
        result[0].FirstName.Should().Be("Alpha");
        result[0].LastName.Should().Be("User");
        result[0].ProfilePhotoUrl.Should().BeNull();
        result[0].FriendsSinceUtc.Should().Be(friendshipA.CreatedAtUtc);

        result[1].UserId.Should().Be(friendBId);
        result[1].UserName.Should().Be("beta_user");
        result[1].FirstName.Should().Be("Beta");
        result[1].LastName.Should().Be("User");
        result[1].ProfilePhotoUrl.Should().Be("https://cdn.example.com/profiles/beta.jpg");
        result[1].FriendsSinceUtc.Should().Be(friendshipB.CreatedAtUtc);
    }

    private static void SetEntityId(User user, Guid id)
    {
        typeof(InMoment.Domain.Common.Entity<Guid>)
            .GetProperty("Id")!
            .SetValue(user, id);
    }

    private static void SetCreatedAtUtc(Friendship friendship, DateTime value)
    {
        typeof(Friendship)
            .GetProperty(nameof(Friendship.CreatedAtUtc))!
            .SetValue(friendship, value);
    }
}