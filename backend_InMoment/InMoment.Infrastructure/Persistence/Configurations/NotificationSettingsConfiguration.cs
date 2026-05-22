using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> b)
    {
        b.ToTable("notification_settings");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.PushEnabled).IsRequired();
        b.Property(x => x.PushGroupInvitations).IsRequired();
        b.Property(x => x.PushReactions).IsRequired();
        b.Property(x => x.PushComments).IsRequired();
        b.Property(x => x.PushReplies).IsRequired();
        b.Property(x => x.PushMentions).IsRequired();
        b.Property(x => x.PushPosts).IsRequired();
        b.Property(x => x.PushRetention).IsRequired();
        b.Property(x => x.PushProductUpdates).IsRequired();

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.UserId).IsUnique();
    }
}