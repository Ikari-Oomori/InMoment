using InMoment.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> b)
    {
        b.ToTable("refresh_sessions");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.DeviceName).HasMaxLength(200);
        b.Property(x => x.Platform).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(100);
        b.Property(x => x.UserAgent).HasMaxLength(1000);

        b.Property(x => x.GeoCountry).HasMaxLength(120);
        b.Property(x => x.GeoRegion).HasMaxLength(120);
        b.Property(x => x.GeoCity).HasMaxLength(120);
        b.Property(x => x.GeoProvider).HasMaxLength(80);

        b.Property(x => x.RevokeReason).HasMaxLength(300);

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ExpiresAtUtc);
        b.HasIndex(x => x.RevokedAtUtc);
    }
}