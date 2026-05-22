using System.Security.Claims;
using FluentAssertions;
using InMoment.API.Realtime;
using InMoment.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace InMoment.Tests.IntegrationTests.Realtime;

public sealed class GroupsHubTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ILogger<GroupsHub>> _logger = new();

    private GroupsHub CreateHub(
        Guid? userId = null,
        string? connectionId = "conn-1",
        TestGroupManager? groupManager = null)
    {
        var hub = new GroupsHub(_groups.Object, _logger.Object)
        {
            Context = new TestHubCallerContext(userId, connectionId ?? "conn-1"),
            Groups = groupManager ?? new TestGroupManager()
        };

        return hub;
    }

    [Fact]
    public async Task JoinGroup_ShouldThrowHubException_WhenGroupIdEmpty()
    {
        var hub = CreateHub(Guid.NewGuid());

        var act = () => hub.JoinGroup(Guid.Empty);

        var ex = await Assert.ThrowsAsync<HubException>(act);
        ex.Message.Should().Be("GroupId is required.");

        _groups.Verify(
            x => x.IsMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinGroup_ShouldThrowHubException_WhenUserUnauthorized()
    {
        var hub = CreateHub(userId: null);

        var act = () => hub.JoinGroup(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<HubException>(act);
        ex.Message.Should().Be("Unauthorized");

        _groups.Verify(
            x => x.IsMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinGroup_ShouldThrowHubException_WhenUserIsNotMember()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var manager = new TestGroupManager();

        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var hub = CreateHub(userId, groupManager: manager);

        var act = () => hub.JoinGroup(groupId);

        var ex = await Assert.ThrowsAsync<HubException>(act);
        ex.Message.Should().Be("You are not an active member of this group.");

        manager.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task JoinGroup_ShouldAddConnectionToSignalRGroup_WhenUserIsMember()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var manager = new TestGroupManager();

        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hub = CreateHub(userId, connectionId: "conn-join", groupManager: manager);

        await hub.JoinGroup(groupId);

        manager.Added.Should().ContainSingle();
        manager.Added[0].ConnectionId.Should().Be("conn-join");
        manager.Added[0].GroupName.Should().Be($"group:{groupId:D}");
    }

    [Fact]
    public async Task LeaveGroup_ShouldThrowHubException_WhenGroupIdEmpty()
    {
        var hub = CreateHub(Guid.NewGuid());

        var act = () => hub.LeaveGroup(Guid.Empty);

        var ex = await Assert.ThrowsAsync<HubException>(act);
        ex.Message.Should().Be("GroupId is required.");
    }

    [Fact]
    public async Task LeaveGroup_ShouldRemoveConnectionFromSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var manager = new TestGroupManager();

        var hub = CreateHub(userId, connectionId: "conn-leave", groupManager: manager);

        await hub.LeaveGroup(groupId);

        manager.Removed.Should().ContainSingle();
        manager.Removed[0].ConnectionId.Should().Be("conn-leave");
        manager.Removed[0].GroupName.Should().Be($"group:{groupId:D}");
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = new();
        public List<(string ConnectionId, string GroupName)> Removed { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal _user;

        public TestHubCallerContext(Guid? userId, string connectionId)
        {
            ConnectionId = connectionId;
            ConnectionAborted = CancellationToken.None;
            UserIdentifier = userId?.ToString();

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }

            _user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            Items = new Dictionary<object, object?>();
            Features = new FeatureCollection();
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier { get; }
        public override ClaimsPrincipal User => _user;
        public override IDictionary<object, object?> Items { get; }
        public override IFeatureCollection Features { get; }
        public override CancellationToken ConnectionAborted { get; }
        public override void Abort() { }
    }
}