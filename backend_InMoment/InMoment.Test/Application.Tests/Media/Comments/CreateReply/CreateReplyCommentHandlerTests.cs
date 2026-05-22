using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.CreateReply;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Media.Comments.CreateReply;

public sealed class CreateReplyCommentHandlerTests
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
        var command = new CreateReplyCommentCommand(Guid.Empty, Guid.NewGuid(), "hello");

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
        var command = new CreateReplyCommentCommand(photoId, Guid.NewGuid(), "hello");

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
        var command = new CreateReplyCommentCommand(photo.Id, Guid.NewGuid(), "hello");

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
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, Guid.NewGuid());

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, Guid.NewGuid(), "hello");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenParentCommentNotFound()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, Guid.NewGuid());
        var parentCommentId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parentCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?)null);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parentCommentId, "hello");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Parent comment not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenParentCommentDeleted()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");
        parent.Delete(parentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "reply");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Parent comment is deleted.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenParentBelongsToAnotherPhoto()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var otherPhotoId = Guid.NewGuid();
        var parent = Comment.CreateRoot(otherPhotoId, parentUserId, "parent");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "reply");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Parent comment belongs to another photo.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUsersAreBlocked()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "reply");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");
    }

    [Fact]
    public async Task Handle_ShouldCreateReply_SaveAndNotifyParentAuthor_WhenValid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");

        Comment? addedReply = null;
        Notification? addedNotification = null;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => addedReply = comment)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                parentUserId,
                NotificationType.ReplyToComment,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotification = notification)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "reply text");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBe(Guid.Empty);

        addedReply.Should().NotBeNull();
        addedReply!.Id.Should().Be(result);
        addedReply.PhotoId.Should().Be(photo.Id);
        addedReply.UserId.Should().Be(currentUserId);
        addedReply.ParentCommentId.Should().Be(parent.Id);
        addedReply.Text.Should().Be("reply text");
        addedReply.IsDeleted.Should().BeFalse();

        addedNotification.Should().NotBeNull();
        addedNotification!.UserId.Should().Be(parentUserId);
        addedNotification.Type.Should().Be(NotificationType.ReplyToComment);
        addedNotification.ActorUserId.Should().Be(currentUserId);
        addedNotification.GroupId.Should().Be(photo.GroupId);
        addedNotification.PhotoId.Should().Be(photo.Id);
        addedNotification.CommentId.Should().Be(result);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(parentUserId, 4, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCollapseReplyNotification_WhenExistingUnreadCollapsibleExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");

        Comment? addedReply = null;
        var collapsed = Notification.CreateReplyToComment(
            userId: parentUserId,
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
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => addedReply = comment)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                parentUserId,
                NotificationType.ReplyToComment,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(collapsed);

        _notifications.Setup(x => x.GetUnreadCountAsync(parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "reply");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(addedReply!.Id);
        collapsed.AggregationCount.Should().Be(oldAggregationCount + 1);
        collapsed.CommentId.Should().Be(result);

        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(parentUserId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotCreateReplyNotification_WhenReplyingToOwnComment()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, Guid.NewGuid());
        var parent = Comment.CreateRoot(photo.Id, currentUserId, "my comment");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "reply to self");

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert
        _notifications.Verify(
            x => x.FindLatestUnreadCollapsibleAsync(
                It.IsAny<Guid>(),
                NotificationType.ReplyToComment,
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
        var parentUserId = Guid.NewGuid();
        var mentionedUser = CreateUser(Guid.NewGuid(), "anna");
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");

        Comment? addedReply = null;
        var addedNotifications = new List<Notification>();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, mentionedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, mentionedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => addedReply = comment)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                parentUserId,
                NotificationType.ReplyToComment,
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

        _notifications.Setup(x => x.GetUnreadCountAsync(parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _notifications.Setup(x => x.GetUnreadCountAsync(mentionedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "hello @anna");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(addedReply!.Id);
        addedNotifications.Should().HaveCount(2);

        addedNotifications.Should().ContainSingle(x =>
            x.UserId == parentUserId &&
            x.Type == NotificationType.ReplyToComment);

        addedNotifications.Should().ContainSingle(x =>
            x.UserId == mentionedUser.Id &&
            x.Type == NotificationType.CommentMention &&
            x.CommentId == result);

        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(parentUserId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(mentionedUser.Id, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSkipMentionNotification_ForSelfParentNonMemberAndBlockedUsers()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var selfUser = CreateUser(currentUserId, "selfuser");
        var parentUser = CreateUser(parentUserId, "parentuser");
        var nonMemberUser = CreateUser(Guid.NewGuid(), "outsider");
        var blockedUser = CreateUser(Guid.NewGuid(), "blocked");
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");

        var addedNotifications = new List<Notification>();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, nonMemberUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.FindLatestUnreadCollapsibleAsync(
                parentUserId,
                NotificationType.ReplyToComment,
                currentUserId,
                photo.GroupId,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        _users.Setup(x => x.GetByUserNameAsync("selfuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(selfUser);
        _users.Setup(x => x.GetByUserNameAsync("parentuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentUser);
        _users.Setup(x => x.GetByUserNameAsync("outsider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonMemberUser);
        _users.Setup(x => x.GetByUserNameAsync("blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockedUser);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotifications.Add(notification))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(
            photo.Id,
            parent.Id,
            "hello @selfuser @parentuser @outsider @blocked");

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert
        addedNotifications.Should().HaveCount(1);
        addedNotifications[0].UserId.Should().Be(parentUserId);
        addedNotifications[0].Type.Should().Be(NotificationType.ReplyToComment);

        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(parentUserId, 1, It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(
                It.Is<Guid>(id => id != parentUserId),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenReplyTextInvalid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, parentUserId);
        var parent = Comment.CreateRoot(photo.Id, parentUserId, "parent");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, parentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new CreateReplyCommentCommand(photo.Id, parent.Id, "   ");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Comment text must be 1..500 characters.");

        _comments.Verify(x => x.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateReplyCommentHandler CreateHandler()
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