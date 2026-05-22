using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Queries;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Notifications.List;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Notifications.List;

public sealed class ListNotificationsHandlerTests
{
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationPreviewReader> _previewReader = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ISystemAnnouncementRepository> _announcements = new();

    private ListNotificationsHandler Create()
        => new(
            _notifications.Object,
            _previewReader.Object,
            _storage.Object,
            _current.Object,
            _announcements.Object);

    [Fact]
    public async Task Handle_ShouldThrow_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new ListNotificationsQuery(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCursorInvalid()
    {
        var userId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);

        var handler = Create();

        var act = () => handler.Handle(
            new ListNotificationsQuery(20, "bad-cursor"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Некорректный формат курсора.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNoItems()
    {
        var userId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);

        _notifications.Setup(x => x.GetPageByUserAsync(
                userId,
                20,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Notification>());

        _notifications.Setup(x => x.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = Create();

        var result = await handler.Handle(
            new ListNotificationsQuery(),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
        result.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitInvalid()
    {
        var userId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);

        _notifications.Setup(x => x.GetPageByUserAsync(
                userId,
                20,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Notification>());

        _notifications.Setup(x => x.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = Create();

        await handler.Handle(
            new ListNotificationsQuery(999, null),
            CancellationToken.None);

        _notifications.Verify(x => x.GetPageByUserAsync(
            userId,
            20,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldParseCursor_AndPassItToRepository()
    {
        var userId = Guid.NewGuid();
        var beforeId = Guid.NewGuid();
        var beforeCreatedAt = DateTime.UtcNow.AddMinutes(-10);

        _current.Setup(x => x.UserId).Returns(userId);

        var cursor = $"{beforeCreatedAt:O}|{beforeId:D}";

        _notifications.Setup(x => x.GetPageByUserAsync(
                userId,
                20,
                It.Is<DateTime?>(x => x.HasValue && x.Value == beforeCreatedAt.ToUniversalTime()),
                It.Is<Guid?>(x => x.HasValue && x.Value == beforeId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Notification>());

        _notifications.Setup(x => x.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = Create();

        await handler.Handle(
            new ListNotificationsQuery(20, cursor),
            CancellationToken.None);

        _notifications.VerifyAll();
    }

    [Fact]
    public async Task Handle_ShouldBuildDtos_AndNextCursor()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        var first = Notification.CreateCommentOnPhoto(
            userId,
            actorId,
            groupId,
            photoId,
            commentId);

        var second = Notification.CreateReactionOnPhoto(
            userId,
            actorId,
            groupId,
            photoId);

        second.CollapseWithLatestOccurrence();
        second.MarkRead();

        _current.Setup(x => x.UserId).Returns(userId);

        _notifications.Setup(x => x.GetPageByUserAsync(
                userId,
                2,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        _notifications.Setup(x => x.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _previewReader.Setup(x => x.GetBundleAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == actorId),
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == groupId),
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photoId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationPreviewBundle(
                new Dictionary<Guid, NotificationActorPreview>
                {
                    [actorId] = new(actorId, "Анна", "anna.user", "https://cdn.example.com/avatars/anna.jpg")
                },
                new Dictionary<Guid, NotificationGroupPreview>
                {
                    [groupId] = new(groupId, "Семья", "https://cdn.example.com/groups/family.jpg")
                },
                new Dictionary<Guid, NotificationPhotoPreview>
                {
                    [photoId] = new(photoId, "groups/family/photos/photo-1.jpg", "семейный вечер")
                }));

        _storage.Setup(x => x.GetPublicUrl("groups/family/photos/photo-1.jpg"))
            .Returns("https://cdn.example.com/groups/family/photos/photo-1.jpg");

        _announcements.Setup(x => x.GetByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, InMoment.Domain.SystemAnnouncements.SystemAnnouncement>());

        var handler = Create();

        var result = await handler.Handle(
            new ListNotificationsQuery(2, null),
            CancellationToken.None);

        result.UnreadCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.NextCursor.Should().NotBeNull();

        var firstItem = result.Items[0];
        firstItem.Id.Should().Be(first.Id);
        firstItem.Type.Should().Be(NotificationType.CommentOnPhoto);
        firstItem.ActorUserId.Should().Be(actorId);
        firstItem.ActorDisplayName.Should().Be("Анна");
        firstItem.ActorUserName.Should().Be("anna.user");
        firstItem.ActorProfilePhotoUrl.Should().Be("https://cdn.example.com/avatars/anna.jpg");
        firstItem.GroupId.Should().Be(groupId);
        firstItem.GroupName.Should().Be("Семья");
        firstItem.GroupAvatarUrl.Should().Be("https://cdn.example.com/groups/family.jpg");
        firstItem.PhotoId.Should().Be(photoId);
        firstItem.PhotoUrl.Should().Be("https://cdn.example.com/groups/family/photos/photo-1.jpg");
        firstItem.ThumbnailUrl.Should().Be("https://cdn.example.com/groups/family/photos/photo-1.jpg");
        firstItem.PhotoCaption.Should().Be("семейный вечер");
        firstItem.CommentId.Should().Be(commentId);
        firstItem.IsRead.Should().BeFalse();
        firstItem.PreviewText.Should().Be("Анна прокомментировал(а) ваше фото");
        firstItem.TargetType.Should().Be(NotificationTargetType.Photo);
        firstItem.TargetId.Should().Be(photoId);
        firstItem.TargetRoute.Should().Be($"/groups/{groupId}/photos/{photoId}");
        firstItem.IsClickable.Should().BeTrue();
        firstItem.CreatedAtHumanized.Should().NotBeNullOrWhiteSpace();

        var secondItem = result.Items[1];
        secondItem.Id.Should().Be(second.Id);
        secondItem.Type.Should().Be(NotificationType.ReactionOnPhoto);
        secondItem.ActorUserName.Should().Be("anna.user");
        secondItem.GroupAvatarUrl.Should().Be("https://cdn.example.com/groups/family.jpg");
        secondItem.PhotoCaption.Should().Be("семейный вечер");
        secondItem.IsRead.Should().BeTrue();
        secondItem.ReadAt.Should().NotBeNull();
        secondItem.AggregationCount.Should().Be(2);
        secondItem.PreviewText.Should().Be("Анна и ещё 1 отреагировали на ваше фото");
        secondItem.TargetType.Should().Be(NotificationTargetType.Photo);
        secondItem.TargetId.Should().Be(photoId);
        secondItem.TargetRoute.Should().Be($"/groups/{groupId}/photos/{photoId}");
        secondItem.IsClickable.Should().BeTrue();

        var expectedCursor = $"{second.CreatedAt.ToUniversalTime():O}|{second.Id:D}";
        result.NextCursor.Should().Be(expectedCursor);
    }
}