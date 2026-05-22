using FluentAssertions;
using InMoment.Application.Abstractions.Media;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.PublishPhoto;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Media.PublishPhoto;

public sealed class PublishPhotoHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationRealtime> _notificationRealtime = new();
    private readonly Mock<INotificationPushDeliveryService> _pushDelivery = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRealtime> _realtime = new();
    private readonly Mock<IVideoProcessingService> _videoProcessing = new();


    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdIsEmpty()
    {
        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            Guid.Empty,
            "groups/x/photos/y/file.jpg",
            "image/jpeg",
            100);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_ShouldThrowValidationException_WhenStorageKeyIsEmpty(string? storageKey)
    {
        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            Guid.NewGuid(),
            storageKey!,
            "image/jpeg",
            100);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("StorageKey is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("image/gif")]
    [InlineData("application/octet-stream")]
    [InlineData("video/avi")]
    public async Task Handle_ShouldThrowValidationException_WhenContentTypeUnsupported(string contentType)
    {
        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            Guid.NewGuid(),
            "groups/x/photos/y/file.jpg",
            contentType,
            100);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Unsupported content type.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(15728641)]
    public async Task Handle_ShouldThrowValidationException_WhenFileSizeInvalid(long sizeBytes)
    {
        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            Guid.NewGuid(),
            "groups/x/photos/y/file.jpg",
            "image/jpeg",
            sizeBytes);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Invalid file size. Maximum allowed size is 15 MB.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserIsNotGroupMember()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            groupId,
            $"groups/{groupId}/photos/{currentUserId}/file.jpg",
            "image/jpeg",
            100);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");

        _photos.Verify(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenStorageKeyDoesNotBelongToUserAndGroup()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            groupId,
            $"groups/{groupId}/photos/{otherUserId}/file.jpg",
            "image/jpeg",
            100);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("StorageKey does not belong to this user/group.");

        _photos.Verify(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreatePhoto_SaveAndNotify_WhenValid()
    {
        var currentUserId = Guid.NewGuid();
        var memberId1 = Guid.NewGuid();
        var memberId2 = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var storageKey = $"groups/{groupId}/photos/{currentUserId}/file.jpg";
        const string contentType = "image/jpeg";
        const long sizeBytes = 1024;

        Photo? addedPhoto = null;
        var addedNotifications = new List<Notification>();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetActiveMemberUserIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentUserId, memberId1, memberId2 });

        _photos.Setup(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()))
            .Callback<Photo, CancellationToken>((photo, _) => addedPhoto = photo)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => addedNotifications.Add(notification))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(memberId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _notifications.Setup(x => x.GetUnreadCountAsync(memberId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new PublishPhotoCommand(groupId, storageKey, contentType, sizeBytes);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);

        addedPhoto.Should().NotBeNull();
        addedPhoto!.Id.Should().Be(result);
        addedPhoto.GroupId.Should().Be(groupId);
        addedPhoto.UploadedByUserId.Should().Be(currentUserId);
        addedPhoto.StorageKey.Should().Be(storageKey);
        addedPhoto.ContentType.Should().Be(contentType);
        addedPhoto.SizeBytes.Should().Be(sizeBytes);
        addedPhoto.IsDeleted.Should().BeFalse();

        addedNotifications.Should().HaveCount(2);
        addedNotifications.Should().OnlyContain(x =>
            x.Type == NotificationType.PhotoPublishedInGroup &&
            x.ActorUserId == currentUserId &&
            x.GroupId == groupId &&
            x.PhotoId == result &&
            x.UserId != currentUserId);

        _photos.Verify(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(groupId, "photo_published", result, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(memberId1, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRealtime.Verify(
            x => x.NotifyNotificationsChangedAsync(memberId2, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _pushDelivery.Verify(
            x => x.TrySendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldAllowAllSupportedContentTypes()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();

        var supportedTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/heic",
            "image/heif",
            "video/mp4",
            "video/quicktime",
            "video/x-m4v",
            "video/webm",
            "video/3gpp"
        };

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetActiveMemberUserIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentUserId, otherMemberId });

        _photos.Setup(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(otherMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();

        foreach (var contentType in supportedTypes)
        {
            var command = new PublishPhotoCommand(
                groupId,
                $"groups/{groupId}/photos/{currentUserId}/{Guid.NewGuid()}.bin",
                contentType,
                1024);

            var result = await handler.Handle(command, CancellationToken.None);
            result.Should().NotBe(Guid.Empty);
        }

        _photos.Verify(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
        _notifications.Verify(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(10));
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(groupId, "photo_published", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(10));
        _videoProcessing.Verify(
            x => x.TrimAndNormalizeAsync(It.IsAny<VideoProcessingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldTrimStorageKeyAndContentType_BeforeValidationAndCreation()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var storageKey = $"groups/{groupId}/photos/{currentUserId}/file.jpg";

        Photo? addedPhoto = null;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetActiveMemberUserIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentUserId, otherMemberId });

        _photos.Setup(x => x.AddAsync(It.IsAny<Photo>(), It.IsAny<CancellationToken>()))
            .Callback<Photo, CancellationToken>((photo, _) => addedPhoto = photo)
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notifications.Setup(x => x.GetUnreadCountAsync(otherMemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new PublishPhotoCommand(
            groupId,
            $"   {storageKey}   ",
            "  image/jpeg  ",
            500);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        addedPhoto.Should().NotBeNull();
        addedPhoto!.StorageKey.Should().Be(storageKey);
        addedPhoto.ContentType.Should().Be("image/jpeg");
    }

    private PublishPhotoHandler CreateHandler()
     => new(
         _current.Object,
         _groups.Object,
         _photos.Object,
         _notifications.Object,
         _notificationRealtime.Object,
         _pushDelivery.Object,
         _uow.Object,
         _realtime.Object,
         _videoProcessing.Object);
}