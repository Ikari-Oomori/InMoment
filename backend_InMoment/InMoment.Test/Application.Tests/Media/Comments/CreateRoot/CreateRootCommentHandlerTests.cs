using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.CreateRoot;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Media.Comments.CreateRoot;

public sealed class CreateRootCommentHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IUserRepository> _users = new();
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
        var command = new CreateRootCommentCommand(Guid.Empty, "hello");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoNotFound()
    {
        // Arrange
        var photoId = Guid.NewGuid();

        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(photoId, "hello");

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
        var command = new CreateRootCommentCommand(photo.Id, "hello");

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
        var command = new CreateRootCommentCommand(photo.Id, "hello");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
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
        var command = new CreateRootCommentCommand(photo.Id, "hello");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");
    }

    [Fact]
    public async Task Handle_ShouldCreateComment_SaveAndNotifyUploader_WhenValid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        Comment? addedComment = null;
        Notification? addedNotification = null;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => addedComment = comment)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                uploaderUserId,
                NotificationType.CommentOnPhoto,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotification = notification)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(photo.Id, "hello world");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBe(Guid.Empty);

        addedComment.Should().NotBeNull();
        addedComment!.Id.Should().Be(result);
        addedComment.PhotoId.Should().Be(photo.Id);
        addedComment.UserId.Should().Be(currentUserId);
        addedComment.ParentCommentId.Should().BeNull();
        addedComment.Text.Should().Be("hello world");
        addedComment.IsDeleted.Should().BeFalse();

        addedNotification.Should().NotBeNull();
        addedNotification!.UserId.Should().Be(uploaderUserId);
        addedNotification.Type.Should().Be(NotificationType.CommentOnPhoto);
        addedNotification.ActorUserId.Should().Be(currentUserId);
        addedNotification.GroupId.Should().Be(photo.GroupId);
        addedNotification.PhotoId.Should().Be(photo.Id);
        addedNotification.CommentId.Should().Be(result);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(uploaderUserId, 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCollapseUploaderNotification_WhenExistingUnreadCollapsibleExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        Comment? addedComment = null;
        var collapsed = Notification.CreateCommentOnPhoto(
            userId: uploaderUserId,
            actorUserId: currentUserId,
            groupId: photo.GroupId,
            photoId: photo.Id,
            commentId: Guid.NewGuid());

        var oldAggregationCount = collapsed.AggregationCount;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => addedComment = comment)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                uploaderUserId,
                NotificationType.CommentOnPhoto,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(collapsed);

        _notifications.Setup(x => x.GetUnreadCountAsync(uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(photo.Id, "new comment");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(addedComment!.Id);
        collapsed.AggregationCount.Should().Be(oldAggregationCount + 1);
        collapsed.CommentId.Should().Be(result);

        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(uploaderUserId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotCreateUploaderNotification_WhenCommentingOwnPhoto()
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
        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(photo.Id, "my own photo");

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

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
    }

    [Fact]
    public async Task Handle_ShouldCreateMentionNotification_ForMentionedMember()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var mentionedUser = CreateUser(Guid.NewGuid(), "anna");
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        Comment? addedComment = null;
        var addedNotifications = new List<Notification>();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, mentionedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, mentionedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => addedComment = comment)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                uploaderUserId,
                NotificationType.CommentOnPhoto,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _users.Setup(x => x.GetByUserNameAsync("anna", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mentionedUser);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                mentionedUser.Id,
                NotificationType.CommentMention,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotifications.Add(notification))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _notifications.Setup(x => x.GetUnreadCountAsync(mentionedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(photo.Id, "hello @anna");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(addedComment!.Id);
        addedNotifications.Should().HaveCount(2);

        addedNotifications.Should().ContainSingle(x =>
            x.UserId == uploaderUserId &&
            x.Type == NotificationType.CommentOnPhoto);

        addedNotifications.Should().ContainSingle(x =>
            x.UserId == mentionedUser.Id &&
            x.Type == NotificationType.CommentMention &&
            x.CommentId == result);

        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(uploaderUserId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(mentionedUser.Id, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSkipMentionNotification_ForSelfUploaderNonMemberAndBlockedUsers()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var selfUser = CreateUser(currentUserId, "selfuser");
        var uploaderUser = CreateUser(uploaderUserId, "owner");
        var nonMemberUser = CreateUser(Guid.NewGuid(), "outsider");
        var blockedUser = CreateUser(Guid.NewGuid(), "blocked");
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        var addedNotifications = new List<Notification>();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, nonMemberUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                uploaderUserId,
                NotificationType.CommentOnPhoto,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _users.Setup(x => x.GetByUserNameAsync("selfuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(selfUser);
        _users.Setup(x => x.GetByUserNameAsync("owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploaderUser);
        _users.Setup(x => x.GetByUserNameAsync("outsider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonMemberUser);
        _users.Setup(x => x.GetByUserNameAsync("blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockedUser);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotifications.Add(notification))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(
            photo.Id,
            "hello @selfuser @owner @outsider @blocked");

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert
        addedNotifications.Should().HaveCount(1);
        addedNotifications[0].UserId.Should().Be(uploaderUserId);
        addedNotifications[0].Type.Should().Be(NotificationType.CommentOnPhoto);

        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(uploaderUserId, 1, It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(
                It.Is<Guid>(id => id != uploaderUserId),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCommentTextInvalid()
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
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new CreateRootCommentCommand(photo.Id, "   ");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Comment text must be 1..500 characters.");

        _comments.Verify(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateRootCommentHandler CreateHandler()
    => new(
        _current.Object,
        _photos.Object,
        _groups.Object,
        _comments.Object,
        _users.Object,
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

    private static User CreateUser(Guid id, string userName)
    {
        var user = User.Create(
            email: $"{userName}@test.com",
            passwordHash: "hash",
            userName: userName,
            firstName: "Test",
            lastName: "User");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);

        return user;
    }
}