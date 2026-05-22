using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.GetUnreadCount;
using Moq;

namespace InMoment.Application.Tests.Notifications.GetUnreadCount;

public sealed class GetUnreadNotificationsCountHandlerTests
{
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetUnreadNotificationsCountHandler Create()
        => new(_notifications.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldReturnUnreadCount()
    {
        var userId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _notifications.Setup(x => x.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var handler = Create();

        var result = await handler.Handle(
            new GetUnreadNotificationsCountQuery(),
            CancellationToken.None);

        result.Count.Should().Be(7);
    }
}