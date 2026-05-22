using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Type).HasConversion<int>().IsRequired();

        b.Property(x => x.ActorUserId);
        b.Property(x => x.GroupId);
        b.Property(x => x.PhotoId);
        b.Property(x => x.CommentId);
        b.Property(x => x.InvitationId);
        b.Property(x => x.SystemMemoryId);
        b.Property(x => x.SystemAnnouncementId);

        b.Property(x => x.IsRead).IsRequired();
        b.Property(x => x.AggregationCount).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ReadAt);

        b.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id })
            .HasDatabaseName("IX_notifications_UserId_CreatedAt_Id");

        b.HasIndex(x => new { x.UserId, x.IsRead })
            .HasDatabaseName("IX_notifications_UserId_IsRead");
    }
}