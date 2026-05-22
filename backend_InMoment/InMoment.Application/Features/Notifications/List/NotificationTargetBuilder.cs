using InMoment.Domain.Notifications;

namespace InMoment.Application.Features.Notifications.List;

internal static class NotificationTargetBuilder
{
    public static (NotificationTargetType targetType, Guid? targetId, string? targetRoute) Build(
        NotificationType type,
        Guid? groupId,
        Guid? photoId,
        Guid? commentId,
        Guid? invitationId,
        Guid? systemMemoryId = null,
        Guid? systemAnnouncementId = null)
    {
        return type switch
        {
            NotificationType.GroupInvitationReceived
                when invitationId.HasValue
                => (
                    NotificationTargetType.Invitation,
                    invitationId.Value,
                    $"/invitations/{invitationId.Value}"
                ),

            NotificationType.ReactionOnPhoto
                when groupId.HasValue && photoId.HasValue
                => (
                    NotificationTargetType.Photo,
                    photoId.Value,
                    $"/groups/{groupId.Value}/photos/{photoId.Value}"
                ),

            NotificationType.CommentOnPhoto
                when groupId.HasValue && photoId.HasValue
                => (
                    NotificationTargetType.Photo,
                    photoId.Value,
                    $"/groups/{groupId.Value}/photos/{photoId.Value}"
                ),

            NotificationType.PhotoPublishedInGroup
                when groupId.HasValue && photoId.HasValue
                => (
                    NotificationTargetType.Photo,
                    photoId.Value,
                    $"/groups/{groupId.Value}/photos/{photoId.Value}"
                ),

            NotificationType.ReplyToComment
                when groupId.HasValue && photoId.HasValue && commentId.HasValue
                => (
                    NotificationTargetType.Comment,
                    commentId.Value,
                    $"/groups/{groupId.Value}/photos/{photoId.Value}?commentId={commentId.Value}"
                ),

            NotificationType.CommentMention
                when groupId.HasValue && photoId.HasValue && commentId.HasValue
                => (
                    NotificationTargetType.Comment,
                    commentId.Value,
                    $"/groups/{groupId.Value}/photos/{photoId.Value}?commentId={commentId.Value}"
                ),

            NotificationType.ReportReviewed
                => (
                    NotificationTargetType.Reports,
                    null,
                    "/reports/my"
                ),

            NotificationType.ReportAppealSubmitted
                => (
                    NotificationTargetType.Reports,
                    null,
                    "/reports/my"
                ),

            NotificationType.ReportAppealReviewed
                => (
                    NotificationTargetType.Reports,
                    null,
                    "/reports/my"
                ),

            NotificationType.ShareReminder
                => (
                    NotificationTargetType.Unknown,
                    null,
                    "/camera"
                ),

            NotificationType.FeedbackPrompt
                => (
                    NotificationTargetType.Unknown,
                    null,
                    "/support/suggestion"
                ),

            NotificationType.Anniversary
                => (
                    NotificationTargetType.Unknown,
                    null,
                    "/memories"
                ),

            NotificationType.SystemMemoryReady
                when systemMemoryId.HasValue
                => (
                    NotificationTargetType.SystemMemory,
                    systemMemoryId.Value,
                    $"/memories/system/{systemMemoryId.Value}"
                ),

            NotificationType.ProductAnnouncement
                => (
                    NotificationTargetType.Unknown,
                    null,
                    "/settings"
                ),

            NotificationType.ModeratorAnnouncement
                when systemAnnouncementId.HasValue
                => (
                    NotificationTargetType.SystemAnnouncement,
                    systemAnnouncementId.Value,
                    $"/announcements/{systemAnnouncementId.Value}"
                ),

            _ => (NotificationTargetType.Unknown, null, null)
        };
    }
}
