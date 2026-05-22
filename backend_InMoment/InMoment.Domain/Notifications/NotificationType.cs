namespace InMoment.Domain.Notifications;

public enum NotificationType
{
    GroupInvitationReceived = 1,
    ReactionOnPhoto = 2,
    CommentOnPhoto = 3,
    ReplyToComment = 4,
    CommentMention = 5,
    PhotoPublishedInGroup = 6,

    ReportReviewed = 7,
    ReportAppealSubmitted = 8,
    ReportAppealReviewed = 9,

    ShareReminder = 10,
    FeedbackPrompt = 11,
    Anniversary = 12,
    ProductAnnouncement = 13,
    SystemMemoryReady = 14,
    ModeratorAnnouncement = 20,
}