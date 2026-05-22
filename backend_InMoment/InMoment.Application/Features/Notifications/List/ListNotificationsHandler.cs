using System.Globalization;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Queries;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Notifications.List;

public sealed class ListNotificationsHandler : IRequestHandler<ListNotificationsQuery, NotificationsPageDto>
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationPreviewReader _previewReader;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;
    private readonly ISystemAnnouncementRepository _announcements;

    public ListNotificationsHandler(
        INotificationRepository notifications,
        INotificationPreviewReader previewReader,
        IFileStorage storage,
        ICurrentUser current,
        ISystemAnnouncementRepository announcements)
    {
        _notifications = notifications;
        _previewReader = previewReader;
        _storage = storage;
        _current = current;
        _announcements = announcements;
    }

    public async Task<NotificationsPageDto> Handle(ListNotificationsQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var limit = q.Limit is < 1 or > 50 ? 20 : q.Limit;

        DateTime? beforeCreatedAt = null;
        Guid? beforeNotificationId = null;

        if (!string.IsNullOrWhiteSpace(q.Cursor))
        {
            if (!TryParseCursor(q.Cursor!, out beforeCreatedAt, out beforeNotificationId))
                throw new ValidationException("Некорректный формат курсора.");
        }

        var items = await _notifications.GetPageByUserAsync(
            _current.UserId,
            limit,
            beforeCreatedAt,
            beforeNotificationId,
            ct);

        var unreadCount = await _notifications.GetUnreadCountAsync(_current.UserId, ct);

        if (items.Count == 0)
            return new NotificationsPageDto(Array.Empty<NotificationDto>(), null, unreadCount);

        var actorIds = items
            .Where(x => x.ActorUserId.HasValue)
            .Select(x => x.ActorUserId!.Value)
            .Distinct()
            .ToList();

        var groupIds = items
            .Where(x => x.GroupId.HasValue)
            .Select(x => x.GroupId!.Value)
            .Distinct()
            .ToList();

        var photoIds = items
            .Where(x => x.PhotoId.HasValue)
            .Select(x => x.PhotoId!.Value)
            .Distinct()
            .ToList();

        var announcementIds = items
            .Where(x => x.SystemAnnouncementId.HasValue)
            .Select(x => x.SystemAnnouncementId!.Value)
            .Distinct()
            .ToList();

        var announcements = await _announcements.GetByIdsAsync(announcementIds, ct);

        var bundle = await _previewReader.GetBundleAsync(actorIds, groupIds, photoIds, ct);

        var dto = items.Select(x =>
        {
            string? actorDisplayName = null;
            string? actorUserName = null;
            string? actorProfilePhotoUrl = null;
            string? groupName = null;
            string? groupAvatarUrl = null;
            string? photoUrl = null;
            string? thumbnailUrl = null;
            string? photoCaption = null;
            string? announcementText = null;
            string? announcementMediaUrl = null;
            string? announcementMediaContentType = null;

            if (x.SystemAnnouncementId.HasValue &&
                announcements.TryGetValue(x.SystemAnnouncementId.Value, out var announcement))
            {
                announcementText = announcement.Text;
                announcementMediaUrl = announcement.MediaUrl;
                announcementMediaContentType = announcement.MediaContentType;
            }

            if (x.ActorUserId.HasValue &&
                bundle.Actors.TryGetValue(x.ActorUserId.Value, out var actor))
            {
                actorDisplayName = actor.DisplayName;
                actorUserName = actor.UserName;
                actorProfilePhotoUrl = actor.ProfilePhotoUrl;
            }

            if (x.GroupId.HasValue &&
                bundle.Groups.TryGetValue(x.GroupId.Value, out var group))
            {
                groupName = group.Name;
                groupAvatarUrl = group.AvatarUrl;
            }

            if (x.PhotoId.HasValue &&
                bundle.Photos.TryGetValue(x.PhotoId.Value, out var photo))
            {
                photoUrl = _storage.GetPublicUrl(photo.StorageKey);
                thumbnailUrl = photoUrl;
                photoCaption = photo.Caption;
            }

            var previewText = x.Type == Domain.Notifications.NotificationType.ModeratorAnnouncement &&
                    !string.IsNullOrWhiteSpace(announcementText)
                  ? announcementText
                  : NotificationTextBuilder.Build(
                      x.Type,
                      actorDisplayName,
                      groupName,
                      x.AggregationCount);

            var (targetType, targetId, targetRoute) = NotificationTargetBuilder.Build(
                x.Type,
                x.GroupId,
                x.PhotoId,
                x.CommentId,
                x.InvitationId,
                x.SystemMemoryId,
                x.SystemAnnouncementId);

            var createdAtHumanized = NotificationTimeTextBuilder.BuildRu(x.CreatedAt);
            var isClickable = !string.IsNullOrWhiteSpace(targetRoute);

            return new NotificationDto(
                x.Id,
                x.Type,
                x.ActorUserId,
                actorDisplayName,
                actorUserName,
                actorProfilePhotoUrl,
                x.GroupId,
                groupName,
                groupAvatarUrl,
                x.PhotoId,
                photoUrl,
                thumbnailUrl,
                photoCaption,
                x.CommentId,
                x.InvitationId,
                x.SystemMemoryId,
                x.SystemAnnouncementId,
                announcementText,
                announcementMediaUrl,
                announcementMediaContentType,
                x.IsRead,
                x.AggregationCount,
                previewText,
                targetType,
                targetId,
                targetRoute,
                isClickable,
                createdAtHumanized,
                x.CreatedAt,
                x.ReadAt
            );
        }).ToList();

        var last = items[^1];
        var nextCursor = items.Count < limit ? null : BuildCursor(last.CreatedAt, last.Id);

        return new NotificationsPageDto(dto, nextCursor, unreadCount);
    }

    private static string BuildCursor(DateTime createdAt, Guid notificationId)
        => $"{createdAt.ToUniversalTime():O}|{notificationId:D}";

    private static bool TryParseCursor(string cursor, out DateTime? createdAt, out Guid? notificationId)
    {
        createdAt = null;
        notificationId = null;

        var parts = cursor.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        if (!DateTime.TryParse(parts[0], null, DateTimeStyles.RoundtripKind, out var dt))
            return false;

        if (!Guid.TryParse(parts[1], out var id))
            return false;

        createdAt = dt.ToUniversalTime();
        notificationId = id;
        return true;
    }
}