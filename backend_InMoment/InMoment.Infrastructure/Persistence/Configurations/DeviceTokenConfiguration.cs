using InMoment.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.ToTable("device_tokens");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(4000);

        b.Property(x => x.Platform)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Provider)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.DeviceName)
            .HasMaxLength(200);

        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
        b.Property(x => x.LastUsedAtUtc).IsRequired();

        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.UserId, x.IsActive })
            .HasDatabaseName("IX_device_tokens_UserId_IsActive");
    }
}