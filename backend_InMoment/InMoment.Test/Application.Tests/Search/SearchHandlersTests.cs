using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Search.Groups;
using InMoment.Application.Features.Search.Mentions;
using InMoment.Application.Features.Search.Users;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Search;

public sealed class SearchHandlersTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();

    private readonly Guid _currentUserId = Guid.NewGuid();

    private SearchMyGroupsHandler CreateGroupsHandler()
        => new(_groups.Object, _current.Object);

    private MentionUsersHandler CreateMentionsHandler()
        => new(_users.Object, _blocks.Object, _groups.Object, _current.Object);

    private SearchUsersHandler CreateUsersHandler()
        => new(_users.Object, _privacy.Object, _blocks.Object, _current.Object);

    [Fact]
    public async Task SearchMyGroups_ShouldReturnEmpty_WhenQueryIsBlank()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateGroupsHandler();

        var result = await handler.Handle(
            new SearchMyGroupsQuery("   "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _groups.Verify(x => x.SearchMyGroupsAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchMyGroups_ShouldNormalizeLimit_AndMapResult()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var g1 = InMoment.Domain.Groups.Group.Create("Family", _currentUserId);
        var g2 = InMoment.Domain.Groups.Group.Create("Friends", _currentUserId);

        _groups.Setup(x => x.SearchMyGroupsAsync(
                _currentUserId,
                "fa",
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { g1, g2 });

        var handler = CreateGroupsHandler();

        var result = await handler.Handle(
            new SearchMyGroupsQuery("  fa  ", 999),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(x => x.Id == g1.Id && x.Name == "Family");
        result.Should().Contain(x => x.Id == g2.Id && x.Name == "Friends");
    }

    [Fact]
    public async Task MentionUsers_ShouldReturnEmpty_WhenQueryIsBlank()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateMentionsHandler();

        var result = await handler.Handle(
            new MentionUsersQuery(" "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(x => x.SearchByPrefixAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MentionUsers_ShouldFilterBlockedUsers_AndBuildDisplayName()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var visible = User.Create(
            "visible@test.com",
            "hash",
            "visible_user",
            "Anna",
            "Petrova");

        var blocked = User.Create(
            "blocked@test.com",
            "hash",
            "blocked_user",
            "Blocked",
            "User");

        _users.Setup(x => x.SearchByPrefixAsync(
                "an",
                15,
                _currentUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visible, blocked });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(_currentUserId, visible.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(_currentUserId, blocked.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateMentionsHandler();

        var result = await handler.Handle(
            new MentionUsersQuery(" an ", 999),
            CancellationToken.None);

        result.Should().HaveCount(1);

        var user = result.Single();
        user.Id.Should().Be(visible.Id);
        user.UserName.Should().Be("visible_user");
        user.DisplayName.Should().Be("Anna Petrova");
    }

    [Fact]
    public async Task MentionUsers_ShouldReturnOnlyGroupMembers_WhenGroupIdProvided()
    {
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var member = User.Create(
            "member@test.com",
            "hash",
            "member_user",
            "Mila",
            "Ivanova");

        var outsider = User.Create(
            "outsider@test.com",
            "hash",
            "outsider_user",
            "Olga",
            "Petrova");

        _groups.Setup(x => x.IsMemberAsync(groupId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetActiveMemberUserIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _currentUserId, member.Id });

        _users.Setup(x => x.SearchByPrefixAsync(
                "mi",
                15,
                _currentUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { member, outsider });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(_currentUserId, member.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateMentionsHandler();

        var result = await handler.Handle(
            new MentionUsersQuery("mi", 5, groupId),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(member.Id);
    }

    [Fact]
    public async Task SearchUsers_ShouldReturnEmpty_WhenQueryIsBlank()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateUsersHandler();

        var result = await handler.Handle(
            new SearchUsersQuery(" "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _users.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchUsers_ShouldFilterBlockedUsers_AndMapVisibleUsers()
    {
        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var visible = User.Create(
            "visible-search@test.com",
            "hash",
            "visible_search",
            "Elena",
            "Sidorova");

        var blocked = User.Create(
            "blocked-search@test.com",
            "hash",
            "blocked_search",
            "Blocked",
            "User");

        _users.Setup(x => x.SearchAsync(
                "el",
                10,
                _currentUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visible, blocked });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(_currentUserId, visible.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(_currentUserId, blocked.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _privacy.Setup(x => x.GetByUserIdAsync(visible.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InMoment.Domain.Privacy.PrivacySettings?)null);

        var handler = CreateUsersHandler();

        var result = await handler.Handle(
            new SearchUsersQuery(" el ", 999),
            CancellationToken.None);

        result.Should().HaveCount(1);

        var user = result.Single();
        user.Id.Should().Be(visible.Id);
        user.UserName.Should().Be("visible_search");
        user.DisplayName.Should().Be("Elena Sidorova");
    }
}