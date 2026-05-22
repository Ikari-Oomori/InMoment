using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.MarkRead;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Notifications.MarkRead;

public sealed class MarkNotificationReadHandlerTests
{
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationRealtime> _notificationRealtime = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private MarkNotificationReadHandler Create()
        => new(
            _notifications.Object,
            _notificationRealtime.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _notifications.Setup(x => x.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new MarkNotificationReadCommand(notificationId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Notification not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationBelongsToAnotherUser()
    {
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        var notification = Notification.CreateCommentOnPhoto(
            ownerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _notifications.Setup(x => x.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var handler = Create();

        var act = () => handler.Handle(
            new MarkNotificationReadCommand(notification.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot modify this notification.");
    }

    [Fact]
    public async Task Handle_ShouldMarkRead_SaveAndNotify()
    {
        var currentUserId = Guid.NewGuid();

        var notification = Notification.CreateCommentOnPhoto(
            currentUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _notifications.Setup(x => x.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        _notifications.Setup(x => x.GetUnreadCountAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = Create();

        var result = await handler.Handle(
            new MarkNotificationReadCommand(notification.Id),
            CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            currentUserId,
            3,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}