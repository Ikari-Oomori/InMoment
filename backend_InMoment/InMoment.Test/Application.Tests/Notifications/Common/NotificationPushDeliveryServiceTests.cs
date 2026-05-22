using FluentAssertions;
using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Queries;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Notifications.Common;

public sealed class NotificationPushDeliveryServiceTests
{
    private readonly Mock<INotificationSettingsRepository> _settings = new();
    private readonly Mock<IDeviceTokenRepository> _tokens = new();
    private readonly Mock<INotificationPreviewReader> _reader = new();
    private readonly Mock<IPushSender> _sender = new();
    private readonly Mock<ISystemAnnouncementRepository> _announcements = new();

    private NotificationPushDeliveryService Create()
        => new(
            _settings.Object,
            _tokens.Object,
            _reader.Object,
            _sender.Object,
            _announcements.Object);

    [Fact]
    public async Task TrySendAsync_ShouldSkip_WhenPushDisabledForType()
    {
        var userId = Guid.NewGuid();
        var settings = NotificationSettings.CreateDefault(userId);
        settings.Update(
            pushEnabled: true,
            pushGroupInvitations: true,
            pushReactions: false,
            pushComments: true,
            pushReplies: true,
            pushMentions: true,
            pushPosts: true,
            pushRetention: true,
            pushProductUpdates: true);

        _settings.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var notification = Notification.CreateReactionOnPhoto(
            userId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        var service = Create();

        await service.TrySendAsync(notification, CancellationToken.None);

        _tokens.Verify(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(x => x.SendAsync(It.IsAny<PushSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TrySendAsync_ShouldSend_WhenEnabledAndHasDevices()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var notification = Notification.CreateCommentOnPhoto(userId, actorId, groupId, photoId, Guid.NewGuid());

        var settings = NotificationSettings.CreateDefault(userId);

        var token = DeviceToken.Register(
            userId,
            "push_token",
            PushPlatform.Android,
            PushProvider.Fcm,
            "Pixel");

        _settings.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _tokens.Setup(x => x.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { token });

        _reader.Setup(x => x.GetBundleAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationPreviewBundle(
                Actors: new Dictionary<Guid, NotificationActorPreview>
                {
                    [actorId] = new(actorId, "Анна Петрова", "anna", null)
                },
                Groups: new Dictionary<Guid, NotificationGroupPreview>
                {
                    [groupId] = new(groupId, "Family", null)
                },
                Photos: new Dictionary<Guid, NotificationPhotoPreview>
                {
                    [photoId] = new(photoId, "key", "caption")
                }));

        var service = Create();

        await service.TrySendAsync(notification, CancellationToken.None);

        _sender.Verify(x => x.SendAsync(
            It.Is<PushSendRequest>(r =>
                r.UserId == userId &&
                r.NotificationType == NotificationType.CommentOnPhoto &&
                r.Targets.Count == 1 &&
                r.Title == "Новый комментарий"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}