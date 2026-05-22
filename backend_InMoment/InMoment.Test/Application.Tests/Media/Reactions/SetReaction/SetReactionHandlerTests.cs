using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Reactions.SetReaction;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;

namespace InMoment.Application.Tests.Media.Reactions.SetReaction;

public sealed class SetReactionHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IReactionRepository> _reactions = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationRealtime> _notificationRealtime = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRealtime> _realtime = new();
    private readonly Mock<INotificationPushDeliveryService> _pushDelivery = new();

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdIsEmpty()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetReactionCommand(Guid.Empty, ReactionType.Heart);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenReactionTypeIsNone()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetReactionCommand(Guid.NewGuid(), ReactionType.None);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("ReactionType is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoNotFound()
    {
        // Arrange
        var photoId = Guid.NewGuid();

        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photoId, ReactionType.Heart);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoIsDeleted()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), ownerUserId, ownerUserId);
        photo.MarkDeleted(ownerUserId, ownerUserId);

        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Heart);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserIsNotGroupMember()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Heart);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");

        _reactions.Verify(x => x.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUsersAreBlocked()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Heart);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");

        _reactions.Verify(x => x.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateReaction_WhenNoExistingReaction()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        Reaction? addedReaction = null;
        Notification? addedNotification = null;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        _reactions.Setup(x => x.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()))
            .Callback<Reaction, CancellationToken>((reaction, _) => addedReaction = reaction)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                uploaderUserId,
                NotificationType.ReactionOnPhoto,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotification = notification)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Wow);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        addedReaction.Should().NotBeNull();
        addedReaction!.PhotoId.Should().Be(photo.Id);
        addedReaction.UserId.Should().Be(currentUserId);
        addedReaction.Type.Should().Be(ReactionType.Wow);

        addedNotification.Should().NotBeNull();
        addedNotification!.UserId.Should().Be(uploaderUserId);
        addedNotification.Type.Should().Be(NotificationType.ReactionOnPhoto);
        addedNotification.ActorUserId.Should().Be(currentUserId);
        addedNotification.GroupId.Should().Be(photo.GroupId);
        addedNotification.PhotoId.Should().Be(photo.Id);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "reaction_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(uploaderUserId, 4, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCollapseNotification_WhenExistingUnreadCollapsibleExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        var collapsed = Notification.CreateReactionOnPhoto(
            userId: uploaderUserId,
            actorUserId: currentUserId,
            groupId: photo.GroupId,
            photoId: photo.Id);

        var oldAggregationCount = collapsed.AggregationCount;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        _reactions.Setup(x => x.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                uploaderUserId,
                NotificationType.ReactionOnPhoto,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(collapsed);

        _notifications.Setup(x => x.GetUnreadCountAsync(uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Heart);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        collapsed.AggregationCount.Should().Be(oldAggregationCount + 1);

        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(uploaderUserId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotCreateNotification_WhenUserReactsToOwnPhoto()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        _reactions.Setup(x => x.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Laugh);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _notifications.Verify(
            x => x.FindLatestUnreadCollapsibleAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.GetUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "reaction_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldChangeExistingReaction_WhenReactionAlreadyExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);
        var existingReaction = Reaction.Create(photo.Id, currentUserId, ReactionType.Heart);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReaction);

        var oldUpdatedAt = existingReaction.UpdatedAt;

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Angry);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        existingReaction.Type.Should().Be(ReactionType.Angry);
        existingReaction.UpdatedAt.Should().BeAfter(oldUpdatedAt);

        _reactions.Verify(x => x.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(
            x => x.FindLatestUnreadCollapsibleAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "reaction_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotNotifyNotifications_WhenExistingReactionChanged()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);
        var existingReaction = Reaction.Create(photo.Id, currentUserId, ReactionType.Sad);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReaction);

        var handler = CreateHandler();
        var command = new SetReactionCommand(photo.Id, ReactionType.Wow);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _notifications.Verify(x => x.GetUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SetReactionHandler CreateHandler()
    => new(
        _current.Object,
        _photos.Object,
        _groups.Object,
        _reactions.Object,
        _notifications.Object,
        _notificationRealtime.Object,
        _pushDelivery.Object,
        _blocks.Object,
        _uow.Object,
        _realtime.Object);

    private static Photo CreatePhoto(Guid groupId, Guid currentUserId, Guid uploadedByUserId)
        => Photo.Create(
            groupId: groupId,
            uploadedByUserId: uploadedByUserId,
            storageKey: $"groups/{groupId}/photos/{uploadedByUserId}/{currentUserId}.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024);
}