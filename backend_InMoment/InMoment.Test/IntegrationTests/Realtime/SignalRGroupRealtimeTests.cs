using FluentAssertions;
using InMoment.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace InMoment.Tests.IntegrationTests.Realtime;

public sealed class SignalRGroupRealtimeTests
{
    [Fact]
    public async Task NotifyFeedChangedAsync_ShouldSendToExpectedSignalRGroup()
    {
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var client = new Mock<IGroupsClient>();
        client.Setup(x => x.FeedChanged(groupId, "photo_deleted", photoId))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<IGroupsClient>>();
        clients.Setup(x => x.Group($"group:{groupId:D}"))
            .Returns(client.Object);

        var hub = new Mock<IHubContext<GroupsHub, IGroupsClient>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        var realtime = new SignalRGroupRealtime(hub.Object);

        await realtime.NotifyFeedChangedAsync(groupId, "photo_deleted", photoId, CancellationToken.None);

        clients.Verify(x => x.Group($"group:{groupId:D}"), Times.Once);
        client.Verify(x => x.FeedChanged(groupId, "photo_deleted", photoId), Times.Once);
    }

    [Fact]
    public async Task NotifyFeedChangedAsync_ShouldAllowNullPhotoId()
    {
        var groupId = Guid.NewGuid();

        var client = new Mock<IGroupsClient>();
        client.Setup(x => x.FeedChanged(groupId, "feed_changed", null))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<IGroupsClient>>();
        clients.Setup(x => x.Group($"group:{groupId:D}"))
            .Returns(client.Object);

        var hub = new Mock<IHubContext<GroupsHub, IGroupsClient>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        var realtime = new SignalRGroupRealtime(hub.Object);

        await realtime.NotifyFeedChangedAsync(groupId, "feed_changed", null, CancellationToken.None);

        client.Verify(x => x.FeedChanged(groupId, "feed_changed", null), Times.Once);
    }
}