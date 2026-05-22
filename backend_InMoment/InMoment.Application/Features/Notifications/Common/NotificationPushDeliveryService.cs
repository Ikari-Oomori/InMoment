using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Queries;
using InMoment.Application.Features.Notifications.List;
using InMoment.Domain.Notifications;

namespace InMoment.Application.Features.Notifications.Common;

public sealed class NotificationPushDeliveryService : INotificationPushDeliveryService
{
    private readonly INotificationSettingsRepository _settings;
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly INotificationPreviewReader _previewReader;
    private readonly IPushSender _pushSender;
    private readonly ISystemAnnouncementRepository _announcements;

    public NotificationPushDeliveryService(
        INotificationSettingsRepository settings,
        IDeviceTokenRepository deviceTokens,
        INotificationPreviewReader previewReader,
        IPushSender pushSender,
        ISystemAnnouncementRepository announcements)
    {
        _settings = settings;
        _deviceTokens = deviceTokens;
        _previewReader = previewReader;
        _pushSender = pushSender;
        _announcements = announcements;
    }

    public async Task TrySendAsync(Notification notification, CancellationToken ct)
    {
        var settings = await _settings.GetByUserIdAsync(notification.UserId, ct)
                       ?? NotificationSettings.CreateDefault(notification.UserId);

        if (!settings.IsPushEnabledFor(notification.Type))
            return;

        var tokens = await _deviceTokens.GetActiveByUserIdAsync(notification.UserId, ct);
        if (tokens.Count == 0)
            return;

        var actorIds = notification.ActorUserId.HasValue
            ? new[] { notification.ActorUserId.Value }
            : Array.Empty<Guid>();

        var groupIds = notification.GroupId.HasValue
            ? new[] { notification.GroupId.Value }
            : Array.Empty<Guid>();

        var photoIds = notification.PhotoId.HasValue
            ? new[] { notification.PhotoId.Value }
            : Array.Empty<Guid>();

        var bundle = await _previewReader.GetBundleAsync(actorIds, groupIds, photoIds, ct);

        bundle.Actors.TryGetValue(notification.ActorUserId ?? Guid.Empty, out var actor);
        bundle.Groups.TryGetValue(notification.GroupId ?? Guid.Empty, out var group);

        var announcement = notification.SystemAnnouncementId.HasValue
            ? await _announcements.GetByIdAsync(notification.SystemAnnouncementId.Value, ct)
            : null;

        var body = notification.Type == NotificationType.ModeratorAnnouncement &&
                   announcement is not null
            ? announcement.Text
            : NotificationTextBuilder.Build(
                notification.Type,
                actor?.DisplayName,
                group?.Name,
                notification.AggregationCount);

        var (targetType, targetId, targetRoute) = NotificationTargetBuilder.Build(
            notification.Type,
            notification.GroupId,
            notification.PhotoId,
            notification.CommentId,
            notification.InvitationId,
            notification.SystemMemoryId,
            notification.SystemAnnouncementId);

        var title = BuildTitle(notification.Type);

        var request = new PushSendRequest(
            UserId: notification.UserId,
            NotificationType: notification.Type,
            Title: title,
            Body: body,
            Targets: tokens.Select(x => new PushSendTarget(
                x.Id,
                x.Token,
                x.Platform,
                x.Provider)).ToList(),
            Data: BuildPayload(notification, targetType, targetId, targetRoute));

        await _pushSender.SendAsync(request, ct);

        foreach (var token in tokens)
            token.MarkUsed();
    }

    private static string BuildTitle(NotificationType type)
    {
        return type switch
        {
            NotificationType.GroupInvitationReceived => "Новое приглашение",
            NotificationType.ReactionOnPhoto => "Новая реакция",
            NotificationType.CommentOnPhoto => "Новый комментарий",
            NotificationType.ReplyToComment => "Новый ответ",
            NotificationType.CommentMention => "Вас упомянули",
            NotificationType.PhotoPublishedInGroup => "Новая публикация",
            NotificationType.ReportReviewed => "Жалоба рассмотрена",
            NotificationType.ReportAppealSubmitted => "Апелляция отправлена",
            NotificationType.ReportAppealReviewed => "Апелляция рассмотрена",
            NotificationType.ShareReminder => "Поделитесь моментом",
            NotificationType.FeedbackPrompt => "Нам важно ваше мнение",
            NotificationType.Anniversary => "InMoment и вы",
            NotificationType.SystemMemoryReady => "Ваше воспоминание готово",
            NotificationType.ProductAnnouncement => "Обновление InMoment",
            NotificationType.ModeratorAnnouncement => "Системное уведомление",
            _ => "InMoment"
        };
    }

    private static IReadOnlyDictionary<string, string> BuildPayload(
        Notification notification,
        NotificationTargetType targetType,
        Guid? targetId,
        string? targetRoute)
    {
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = notification.Type.ToString(),
            ["targetType"] = ((int)targetType).ToString()
        };

        if (notification.ActorUserId.HasValue)
            data["actorUserId"] = notification.ActorUserId.Value.ToString();

        if (targetId.HasValue)
            data["targetId"] = targetId.Value.ToString();

        if (!string.IsNullOrWhiteSpace(targetRoute))
            data["targetRoute"] = targetRoute!;

        if (notification.GroupId.HasValue)
            data["groupId"] = notification.GroupId.Value.ToString();

        if (notification.PhotoId.HasValue)
            data["photoId"] = notification.PhotoId.Value.ToString();

        if (notification.CommentId.HasValue)
            data["commentId"] = notification.CommentId.Value.ToString();

        if (notification.SystemMemoryId.HasValue)
            data["systemMemoryId"] = notification.SystemMemoryId.Value.ToString();

        if (notification.SystemAnnouncementId.HasValue)
            data["systemAnnouncementId"] = notification.SystemAnnouncementId.Value.ToString();

        if (notification.InvitationId.HasValue)
            data["invitationId"] = notification.InvitationId.Value.ToString();

        return data;
    }
}