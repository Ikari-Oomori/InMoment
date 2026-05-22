using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class SystemNotificationStateConfiguration : IEntityTypeConfiguration<SystemNotificationState>
{
    public void Configure(EntityTypeBuilder<SystemNotificationState> b)
    {
        b.ToTable("system_notification_states");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.LastShareReminderSentAtUtc);
        b.Property(x => x.LastFeedbackPromptSentAtUtc);
        b.Property(x => x.LastAnniversaryYearSent);

        b.Property(x => x.LastProductAnnouncementKey)
            .HasMaxLength(128);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.UserId).IsUnique();
    }
}