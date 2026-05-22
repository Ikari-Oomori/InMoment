using InMoment.Domain.Common;

namespace InMoment.Domain.Notifications;

public sealed class NotificationSettings : Entity<Guid>
{
    public Guid UserId { get; private set; }

    public bool PushEnabled { get; private set; }

    public bool PushGroupInvitations { get; private set; }
    public bool PushReactions { get; private set; }
    public bool PushComments { get; private set; }
    public bool PushReplies { get; private set; }
    public bool PushMentions { get; private set; }
    public bool PushPosts { get; private set; }

    public bool PushRetention { get; private set; }
    public bool PushProductUpdates { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private NotificationSettings() { }

    public static NotificationSettings CreateDefault(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var now = DateTime.UtcNow;

        return new NotificationSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PushEnabled = true,
            PushGroupInvitations = true,
            PushReactions = true,
            PushComments = true,
            PushReplies = true,
            PushMentions = true,
            PushPosts = true,
            PushRetention = true,
            PushProductUpdates = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(
        bool pushEnabled,
        bool pushGroupInvitations,
        bool pushReactions,
        bool pushComments,
        bool pushReplies,
        bool pushMentions,
        bool pushPosts,
        bool pushRetention,
        bool pushProductUpdates)
    {
        PushEnabled = pushEnabled;
        PushGroupInvitations = pushGroupInvitations;
        PushReactions = pushReactions;
        PushComments = pushComments;
        PushReplies = pushReplies;
        PushMentions = pushMentions;
        PushPosts = pushPosts;
        PushRetention = pushRetention;
        PushProductUpdates = pushProductUpdates;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsPushEnabledFor(NotificationType type)
    {
        if (!PushEnabled)
            return false;

        return type switch
        {
            NotificationType.GroupInvitationReceived => PushGroupInvitations,
            NotificationType.ReactionOnPhoto => PushReactions,
            NotificationType.CommentOnPhoto => PushComments,
            NotificationType.ReplyToComment => PushReplies,
            NotificationType.CommentMention => PushMentions,
            NotificationType.PhotoPublishedInGroup => PushPosts,

            NotificationType.ShareReminder => PushRetention,
            NotificationType.FeedbackPrompt => PushRetention,
            NotificationType.Anniversary => PushRetention,
            NotificationType.SystemMemoryReady => PushRetention,
            NotificationType.ModeratorAnnouncement => PushProductUpdates,
            NotificationType.ProductAnnouncement => PushProductUpdates,

            NotificationType.ReportReviewed => true,
            NotificationType.ReportAppealSubmitted => true,
            NotificationType.ReportAppealReviewed => true,

            _ => false
        };
    }
}