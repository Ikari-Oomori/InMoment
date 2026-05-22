using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Search.Mentions;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Search.Mentions;

public sealed class MentionUsersHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICurrentUser> _current = new();

    private MentionUsersHandler Create()
        => new(_users.Object, _blocks.Object, _groups.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenQueryBlank()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        var result = await handler.Handle(
            new MentionUsersQuery("   "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchByPrefixAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitOutOfRange()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchByPrefixAsync("an", 15, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = Create();

        var result = await handler.Handle(
            new MentionUsersQuery("an", 999),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchByPrefixAsync("an", 15, currentUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFilterBlockedUsers_AndBuildDisplayName()
    {
        var currentUserId = Guid.NewGuid();
        var visibleUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var visibleUser = User.Create("visible@test.com", "hash", "visible_user", "Anna", "Petrova");
        var blockedUser = User.Create("blocked@test.com", "hash", "blocked_user", "Blocked", "User");

        SetEntityId(visibleUser, visibleUserId);
        SetEntityId(blockedUser, blockedUserId);

        visibleUser.SetProfilePhoto("https://cdn.example.com/profiles/anna.jpg");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchByPrefixAsync("an", 15, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visibleUser, blockedUser });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visibleUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var result = await handler.Handle(
            new MentionUsersQuery("an"),
            CancellationToken.None);

        result.Should().HaveCount(1);

        result[0].Id.Should().Be(visibleUserId);
        result[0].UserName.Should().Be("visible_user");
        result[0].DisplayName.Should().Be("Anna Petrova");
        result[0].ProfilePhotoUrl.Should().Be("https://cdn.example.com/profiles/anna.jpg");
    }

    [Fact]
    public async Task Handle_ShouldFallbackToUserName_WhenNameIsEmpty()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var user = User.Create("user@test.com", "hash", "nickname_only", "Temp", "User");
        SetEntityId(user, targetUserId);
        SetStringProperty(user, nameof(User.FirstName), "");
        SetStringProperty(user, nameof(User.LastName), "");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _users.Setup(x => x.SearchByPrefixAsync("ni", 15, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new MentionUsersQuery("ni"),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].DisplayName.Should().Be("nickname_only");
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyGroupMembers_WhenGroupIdProvided()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var outsiderUserId = Guid.NewGuid();

        var member = User.Create("member@test.com", "hash", "member_user", "Mila", "Ivanova");
        var outsider = User.Create("outsider@test.com", "hash", "outsider_user", "Olga", "Petrova");

        SetEntityId(member, memberUserId);
        SetEntityId(outsider, outsiderUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetActiveMemberUserIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentUserId, memberUserId });

        _users.Setup(x => x.SearchByPrefixAsync("m", 15, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { member, outsider });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, memberUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new MentionUsersQuery("m", 5, groupId),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(memberUserId);
        result[0].UserName.Should().Be("member_user");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenCurrentUserIsNotMemberOfGroup()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var result = await handler.Handle(
            new MentionUsersQuery("an", 5, groupId),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(
            x => x.SearchByPrefixAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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