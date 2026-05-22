using InMoment.Domain.Common;

namespace InMoment.Domain.Notifications;

public sealed class Notification : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? PhotoId { get; private set; }
    public Guid? CommentId { get; private set; }
    public Guid? InvitationId { get; private set; }
    public Guid? SystemMemoryId { get; private set; }
    public bool IsRead { get; private set; }
    public int AggregationCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public Guid? SystemAnnouncementId { get; private set; }

    private Notification() { }

    public static Notification CreateGroupInvitationReceived(
        Guid userId,
        Guid actorUserId,
        Guid groupId,
        Guid invitationId)
    {
        return Create(
            userId: userId,
            type: NotificationType.GroupInvitationReceived,
            actorUserId: actorUserId,
            groupId: groupId,
            invitationId: invitationId);
    }

    public static Notification CreateReactionOnPhoto(
        Guid userId,
        Guid actorUserId,
        Guid groupId,
        Guid photoId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ReactionOnPhoto,
            actorUserId: actorUserId,
            groupId: groupId,
            photoId: photoId);
    }

    public static Notification CreateCommentOnPhoto(
        Guid userId,
        Guid actorUserId,
        Guid groupId,
        Guid photoId,
        Guid commentId)
    {
        return Create(
            userId: userId,
            type: NotificationType.CommentOnPhoto,
            actorUserId: actorUserId,
            groupId: groupId,
            photoId: photoId,
            commentId: commentId);
    }

    public static Notification CreateReplyToComment(
        Guid userId,
        Guid actorUserId,
        Guid groupId,
        Guid photoId,
        Guid commentId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ReplyToComment,
            actorUserId: actorUserId,
            groupId: groupId,
            photoId: photoId,
            commentId: commentId);
    }

    public static Notification CreateCommentMention(
        Guid userId,
        Guid actorUserId,
        Guid groupId,
        Guid photoId,
        Guid commentId)
    {
        return Create(
            userId: userId,
            type: NotificationType.CommentMention,
            actorUserId: actorUserId,
            groupId: groupId,
            photoId: photoId,
            commentId: commentId);
    }

    public static Notification CreatePhotoPublishedInGroup(
        Guid userId,
        Guid actorUserId,
        Guid groupId,
        Guid photoId)
    {
        return Create(
            userId: userId,
            type: NotificationType.PhotoPublishedInGroup,
            actorUserId: actorUserId,
            groupId: groupId,
            photoId: photoId);
    }

    public static Notification CreateReportReviewed(
        Guid userId,
        Guid actorUserId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ReportReviewed,
            actorUserId: actorUserId);
    }

    public static Notification CreateReportAppealSubmitted(
        Guid userId,
        Guid actorUserId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ReportAppealSubmitted,
            actorUserId: actorUserId);
    }

    public static Notification CreateReportAppealReviewed(
        Guid userId,
        Guid actorUserId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ReportAppealReviewed,
            actorUserId: actorUserId);
    }

    public static Notification CreateShareReminder(Guid userId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ShareReminder);
    }

    public static Notification CreateFeedbackPrompt(Guid userId)
    {
        return Create(
            userId: userId,
            type: NotificationType.FeedbackPrompt);
    }

    public static Notification CreateAnniversary(Guid userId)
    {
        return Create(
            userId: userId,
            type: NotificationType.Anniversary);
    }

    public static Notification CreateSystemMemoryReady(Guid userId, Guid systemMemoryId)
    {
        return Create(
            userId: userId,
            type: NotificationType.SystemMemoryReady,
            systemMemoryId: systemMemoryId);
    }

    public static Notification CreateProductAnnouncement(Guid userId)
    {
        return Create(
            userId: userId,
            type: NotificationType.ProductAnnouncement);
    }

    public static Notification CreateModeratorAnnouncement(
        Guid userId,
        Guid systemAnnouncementId)
    {
        if (systemAnnouncementId == Guid.Empty)
            throw new ValidationException("SystemAnnouncementId is required.");

        var notification = Create(
            userId: userId,
            type: NotificationType.ModeratorAnnouncement);

        notification.SystemAnnouncementId = systemAnnouncementId;

        return notification;
    }

    private static Notification Create(
        Guid userId,
        NotificationType type,
        Guid? actorUserId = null,
        Guid? groupId = null,
        Guid? photoId = null,
        Guid? commentId = null,
        Guid? invitationId = null,
        Guid? systemMemoryId = null)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            ActorUserId = actorUserId,
            GroupId = groupId,
            PhotoId = photoId,
            CommentId = commentId,
            InvitationId = invitationId,
            SystemMemoryId = systemMemoryId,
            IsRead = false,
            AggregationCount = 1,
            CreatedAt = DateTime.UtcNow,
            ReadAt = null
        };
    }

    public void MarkRead()
    {
        if (IsRead) return;

        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }

    public void CollapseWithLatestOccurrence(
        Guid? latestCommentId = null,
        Guid? latestInvitationId = null)
    {
        if (IsRead)
            throw new ValidationException("Cannot collapse into a read notification.");

        AggregationCount++;

        if (latestCommentId.HasValue)
            CommentId = latestCommentId;

        if (latestInvitationId.HasValue)
            InvitationId = latestInvitationId;

        CreatedAt = DateTime.UtcNow;
    }
}