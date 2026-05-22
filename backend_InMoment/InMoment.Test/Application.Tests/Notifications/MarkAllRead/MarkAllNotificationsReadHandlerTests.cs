using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.MarkAllRead;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Notifications.MarkAllRead;

public sealed class MarkAllNotificationsReadHandlerTests
{
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationRealtime> _notificationRealtime = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private MarkAllNotificationsReadHandler Create()
        => new(
            _notifications.Object,
            _notificationRealtime.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldMarkAllUnread_SaveAndNotify()
    {
        var currentUserId = Guid.NewGuid();

        var first = Notification.CreateCommentOnPhoto(
            currentUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        var second = Notification.CreateReactionOnPhoto(
            currentUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _notifications.Setup(x => x.GetUnreadByUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var handler = Create();

        var result = await handler.Handle(
            new MarkAllNotificationsReadCommand(),
            CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);

        first.IsRead.Should().BeTrue();
        second.IsRead.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            currentUserId,
            0,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldStillSaveAndNotify_WhenUnreadListEmpty()
    {
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _notifications.Setup(x => x.GetUnreadByUserAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Notification>());

        var handler = Create();

        var result = await handler.Handle(
            new MarkAllNotificationsReadCommand(),
            CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            currentUserId,
            0,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}