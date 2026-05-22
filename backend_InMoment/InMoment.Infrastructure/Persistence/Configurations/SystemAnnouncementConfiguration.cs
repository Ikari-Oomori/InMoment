using InMoment.Domain.SystemAnnouncements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class SystemAnnouncementConfiguration : IEntityTypeConfiguration<SystemAnnouncement>
{
    public void Configure(EntityTypeBuilder<SystemAnnouncement> b)
    {
        b.ToTable("system_announcements");
        b.HasKey(x => x.Id);

        b.Property(x => x.CreatedByUserId).IsRequired();

        b.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(2000);

        b.Property(x => x.MediaUrl)
            .HasMaxLength(500);

        b.Property(x => x.MediaContentType)
            .HasMaxLength(100);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc);

        b.HasIndex(x => new { x.CreatedAtUtc, x.Id })
            .HasDatabaseName("IX_system_announcements_CreatedAtUtc_Id");
    }
}