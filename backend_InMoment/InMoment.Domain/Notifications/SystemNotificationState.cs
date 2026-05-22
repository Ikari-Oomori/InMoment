using InMoment.Domain.Common;

namespace InMoment.Domain.Notifications;

public sealed class SystemNotificationState : Entity<Guid>
{
    public Guid UserId { get; private set; }

    public DateTime? LastShareReminderSentAtUtc { get; private set; }
    public DateTime? LastFeedbackPromptSentAtUtc { get; private set; }
    public int? LastAnniversaryYearSent { get; private set; }
    public string? LastProductAnnouncementKey { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private SystemNotificationState() { }

    public static SystemNotificationState Create(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var now = DateTime.UtcNow;

        return new SystemNotificationState
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void MarkShareReminderSent(DateTime nowUtc)
    {
        LastShareReminderSentAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkFeedbackPromptSent(DateTime nowUtc)
    {
        LastFeedbackPromptSentAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkAnniversarySent(int year, DateTime nowUtc)
    {
        LastAnniversaryYearSent = year;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkProductAnnouncementSent(string announcementKey, DateTime nowUtc)
    {
        LastProductAnnouncementKey = string.IsNullOrWhiteSpace(announcementKey)
            ? null
            : announcementKey.Trim();

        UpdatedAtUtc = nowUtc;
    }
}