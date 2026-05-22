using InMoment.Domain.Notifications;

namespace InMoment.Application.Features.Notifications.List;

public enum NotificationTargetType
{
    Unknown = 0,
    Invitation = 1,
    Photo = 2,
    Comment = 3,
    Reports = 4,
    SystemMemory = 5,
    SystemAnnouncement = 6
}

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string? ActorUserName,
    string? ActorProfilePhotoUrl,
    Guid? GroupId,
    string? GroupName,
    string? GroupAvatarUrl,
    Guid? PhotoId,
    string? PhotoUrl,
    string? ThumbnailUrl,
    string? PhotoCaption,
    Guid? CommentId,
    Guid? InvitationId,
    Guid? SystemMemoryId,
    Guid? SystemAnnouncementId,
    string? AnnouncementText,
    string? AnnouncementMediaUrl,
    string? AnnouncementMediaContentType,
    bool IsRead,
    int AggregationCount,
    string PreviewText,
    NotificationTargetType TargetType,
    Guid? TargetId,
    string? TargetRoute,
    bool IsClickable,
    string CreatedAtHumanized,
    DateTime CreatedAt,
    DateTime? ReadAt
);

public sealed record NotificationsPageDto(
    IReadOnlyList<NotificationDto> Items,
    string? NextCursor,
    int UnreadCount
);