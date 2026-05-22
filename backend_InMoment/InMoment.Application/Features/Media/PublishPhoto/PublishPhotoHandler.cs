using InMoment.Application.Abstractions.Media;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Media.PublishPhoto;

public sealed class PublishPhotoHandler : IRequestHandler<PublishPhotoCommand, Guid>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
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
        "video/3gpp",
    };

    private const long MaxImageBytes = 15L * 1024 * 1024;
    private const long MaxVideoBytes = 200L * 1024 * 1024;
    private const long MaxVideoTrimDurationMs = 2L * 60L * 1000L;

    private readonly ICurrentUser _current;
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;
    private readonly IVideoProcessingService _videoProcessing;

    public PublishPhotoHandler(
        ICurrentUser current,
        IGroupRepository groups,
        IPhotoRepository photos,
        INotificationRepository notifications,
        INotificationRealtime notificationRealtime,
        INotificationPushDeliveryService pushDelivery,
        IUnitOfWork uow,
        IGroupRealtime realtime,
        IVideoProcessingService videoProcessing)
    {
        _current = current;
        _groups = groups;
        _photos = photos;
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _pushDelivery = pushDelivery;
        _uow = uow;
        _realtime = realtime;
        _videoProcessing = videoProcessing;
    }

    public async Task<Guid> Handle(PublishPhotoCommand cmd, CancellationToken ct)
    {
        if (cmd.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var storageKey = (cmd.StorageKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ValidationException("StorageKey is required.");

        var contentType = (cmd.ContentType ?? string.Empty).Trim();
        if (!AllowedContentTypes.Contains(contentType))
            throw new ValidationException("Unsupported content type.");

        if (!IsValidSize(contentType, cmd.SizeBytes))
        {
            var limitMb = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                ? MaxVideoBytes / 1024 / 1024
                : MaxImageBytes / 1024 / 1024;

            throw new ValidationException($"Invalid file size. Maximum allowed size is {limitMb} MB.");
        }

        var isMember = await _groups.IsMemberAsync(cmd.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        var expectedPrefix = $"groups/{cmd.GroupId}/photos/{_current.UserId}/";
        if (!storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            throw new ForbiddenException("StorageKey does not belong to this user/group.");

        var finalStorageKey = storageKey;
        var finalContentType = contentType;
        var finalSizeBytes = cmd.SizeBytes;

        if (IsVideo(contentType) && HasTrim(cmd))
        {
            ValidateTrim(cmd);

            var processedStorageKey = BuildProcessedVideoStorageKey(storageKey);

            var processed = await _videoProcessing.TrimAndNormalizeAsync(
                new VideoProcessingRequest(
                    SourceStorageKey: storageKey,
                    TargetStorageKey: processedStorageKey,
                    TrimStartMs: cmd.TrimStartMs!.Value,
                    TrimEndMs: cmd.TrimEndMs!.Value),
                ct);

            finalStorageKey = processed.StorageKey;
            finalContentType = processed.ContentType;
            finalSizeBytes = processed.SizeBytes;
        }
        else if (!IsVideo(contentType) && HasTrim(cmd))
        {
            throw new ValidationException("Trim parameters are only supported for video.");
        }

        var photo = Photo.Create(
            groupId: cmd.GroupId,
            uploadedByUserId: _current.UserId,
            storageKey: finalStorageKey,
            contentType: finalContentType,
            sizeBytes: finalSizeBytes,
            caption: cmd.Caption);

        await _photos.AddAsync(photo, ct);

        var memberIds = await _groups.GetActiveMemberUserIdsAsync(cmd.GroupId, ct);

        var targetUserIds = memberIds
            .Where(x => x != Guid.Empty && x != _current.UserId)
            .Distinct()
            .ToList();

        var createdNotifications = new List<Notification>(targetUserIds.Count);

        foreach (var userId in targetUserIds)
        {
            var notification = Notification.CreatePhotoPublishedInGroup(
                userId: userId,
                actorUserId: _current.UserId,
                groupId: cmd.GroupId,
                photoId: photo.Id);

            await _notifications.AddAsync(notification, ct);
            createdNotifications.Add(notification);
        }

        await _uow.SaveChangesAsync(ct);

        await _realtime.NotifyFeedChangedAsync(
            cmd.GroupId,
            reason: "photo_published",
            photoId: photo.Id,
            ct);

        foreach (var notification in createdNotifications)
        {
            var unreadCount = await _notifications.GetUnreadCountAsync(notification.UserId, ct);
            await _notificationRealtime.NotifyNotificationsChangedAsync(notification.UserId, unreadCount, ct);
            await _pushDelivery.TrySendAsync(notification, ct);
        }

        return photo.Id;
    }

    private static bool IsValidSize(string contentType, long sizeBytes)
    {
        if (sizeBytes <= 0)
            return false;

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return sizeBytes <= MaxImageBytes;

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return sizeBytes <= MaxVideoBytes;

        return false;
    }

    private static bool IsVideo(string contentType)
        => contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    private static bool HasTrim(PublishPhotoCommand cmd)
        => cmd.TrimStartMs.HasValue || cmd.TrimEndMs.HasValue;

    private static void ValidateTrim(PublishPhotoCommand cmd)
    {
        if (!cmd.TrimStartMs.HasValue || !cmd.TrimEndMs.HasValue)
            throw new ValidationException("Both TrimStartMs and TrimEndMs are required.");

        if (cmd.TrimStartMs.Value < 0)
            throw new ValidationException("TrimStartMs must be greater than or equal to zero.");

        if (cmd.TrimEndMs.Value <= cmd.TrimStartMs.Value)
            throw new ValidationException("TrimEndMs must be greater than TrimStartMs.");

        var duration = cmd.TrimEndMs.Value - cmd.TrimStartMs.Value;

        if (duration > MaxVideoTrimDurationMs)
            throw new ValidationException("Video fragment must be no longer than 2 minutes.");
    }

    private static string BuildProcessedVideoStorageKey(string sourceStorageKey)
    {
        var normalized = sourceStorageKey.Trim().TrimStart('/');
        var slashIndex = normalized.LastIndexOf('/');

        if (slashIndex < 0)
            return $"{Path.GetFileNameWithoutExtension(normalized)}.processed.{Guid.NewGuid():N}.mp4";

        var directory = normalized[..(slashIndex + 1)];
        var fileName = normalized[(slashIndex + 1)..];
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        return $"{directory}{fileNameWithoutExtension}.processed.{Guid.NewGuid():N}.mp4";
    }
}