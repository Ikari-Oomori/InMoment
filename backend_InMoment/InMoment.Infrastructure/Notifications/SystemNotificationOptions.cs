namespace InMoment.Infrastructure.Notifications;

public sealed class SystemNotificationOptions
{
    public const string SectionName = "SystemNotifications";

    public bool Enabled { get; set; } = true;

    public int RunOnStartupDelaySeconds { get; set; } = 15;
    public int IntervalMinutes { get; set; } = 360;

    public int ShareReminderAfterDays { get; set; } = 7;
    public int ShareReminderCooldownDays { get; set; } = 7;

    public int FeedbackPromptAfterDays { get; set; } = 14;
    public int FeedbackPromptCooldownDays { get; set; } = 90;

    // Только для локальной проверки. В production должно быть false.
    public bool DevForceSystemMemories { get; set; } = false;

    public ProductAnnouncementOptions ProductAnnouncement { get; set; } = new();
}

public sealed class ProductAnnouncementOptions
{
    public bool Enabled { get; set; } = false;
    public string? CurrentKey { get; set; }
}
