using InMoment.API.Realtime;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace InMoment.Tests.IntegrationTests.Realtime;

public sealed class SignalRNotificationRealtimeTests
{
    [Fact]
    public async Task NotifyNotificationsChangedAsync_ShouldSendToExpectedUserGroup()
    {
        var userId = Guid.NewGuid();

        var client = new Mock<IUsersClient>();
        client.Setup(x => x.NotificationsChanged(7))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<IUsersClient>>();
        clients.Setup(x => x.Group($"user:{userId:D}"))
            .Returns(client.Object);

        var hub = new Mock<IHubContext<UsersHub, IUsersClient>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        var realtime = new SignalRNotificationRealtime(hub.Object);

        await realtime.NotifyNotificationsChangedAsync(userId, 7, CancellationToken.None);

        clients.Verify(x => x.Group($"user:{userId:D}"), Times.Once);
        client.Verify(x => x.NotificationsChanged(7), Times.Once);
    }

    [Fact]
    public async Task NotifyNotificationsChangedAsync_ShouldSendZeroUnreadCount()
    {
        var userId = Guid.NewGuid();

        var client = new Mock<IUsersClient>();
        client.Setup(x => x.NotificationsChanged(0))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<IUsersClient>>();
        clients.Setup(x => x.Group($"user:{userId:D}"))
            .Returns(client.Object);

        var hub = new Mock<IHubContext<UsersHub, IUsersClient>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        var realtime = new SignalRNotificationRealtime(hub.Object);

        await realtime.NotifyNotificationsChangedAsync(userId, 0, CancellationToken.None);

        client.Verify(x => x.NotificationsChanged(0), Times.Once);
    }
}