using System.Security.Claims;
using FluentAssertions;
using InMoment.API.Realtime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace InMoment.Tests.IntegrationTests.Realtime;

public sealed class UsersHubTests
{
    [Fact]
    public async Task JoinSelf_ShouldThrowHubException_WhenUserUnauthorized()
    {
        var manager = new TestGroupManager();
        var hub = CreateHub(userId: null, groupManager: manager);

        var act = () => hub.JoinSelf();

        var ex = await Assert.ThrowsAsync<HubException>(act);
        ex.Message.Should().Be("Unauthorized");

        manager.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task JoinSelf_ShouldAddConnectionToOwnUserGroup()
    {
        var userId = Guid.NewGuid();
        var manager = new TestGroupManager();
        var hub = CreateHub(userId, connectionId: "user-conn", groupManager: manager);

        await hub.JoinSelf();

        manager.Added.Should().ContainSingle();
        manager.Added[0].ConnectionId.Should().Be("user-conn");
        manager.Added[0].GroupName.Should().Be($"user:{userId:D}");
    }

    [Fact]
    public async Task LeaveSelf_ShouldThrowHubException_WhenUserUnauthorized()
    {
        var manager = new TestGroupManager();
        var hub = CreateHub(userId: null, groupManager: manager);

        var act = () => hub.LeaveSelf();

        var ex = await Assert.ThrowsAsync<HubException>(act);
        ex.Message.Should().Be("Unauthorized");

        manager.Removed.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveSelf_ShouldRemoveConnectionFromOwnUserGroup()
    {
        var userId = Guid.NewGuid();
        var manager = new TestGroupManager();
        var hub = CreateHub(userId, connectionId: "user-conn", groupManager: manager);

        await hub.LeaveSelf();

        manager.Removed.Should().ContainSingle();
        manager.Removed[0].ConnectionId.Should().Be("user-conn");
        manager.Removed[0].GroupName.Should().Be($"user:{userId:D}");
    }

    private static UsersHub CreateHub(
        Guid? userId = null,
        string? connectionId = "conn-1",
        TestGroupManager? groupManager = null)
    {
        return new UsersHub
        {
            Context = new TestHubCallerContext(userId, connectionId ?? "conn-1"),
            Groups = groupManager ?? new TestGroupManager()
        };
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