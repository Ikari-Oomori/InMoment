using InMoment.Domain.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class PrivacySettingsConfiguration : IEntityTypeConfiguration<PrivacySettings>
{
    public void Configure(EntityTypeBuilder<PrivacySettings> b)
    {
        b.ToTable("privacy_settings");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.AllowFriendRequestsFrom).IsRequired();
        b.Property(x => x.AllowGroupInvitesFrom).IsRequired();
        b.Property(x => x.DiscoverableByContacts).IsRequired();
        b.Property(x => x.DiscoverableBySearch).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.UserId).IsUnique();
    }
}