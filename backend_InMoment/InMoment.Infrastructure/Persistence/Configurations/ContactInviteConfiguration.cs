using InMoment.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class ContactInviteConfiguration : IEntityTypeConfiguration<ContactInvite>
{
    public void Configure(EntityTypeBuilder<ContactInvite> b)
    {
        b.ToTable("contact_invites");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.Channel)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Email)
            .HasMaxLength(256);

        b.Property(x => x.PhoneNumber)
            .HasMaxLength(32);

        b.Property(x => x.DisplayName)
            .HasMaxLength(200);

        b.Property(x => x.InviteToken)
            .IsRequired()
            .HasMaxLength(200);

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.CancelledAtUtc);

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.InviteToken).IsUnique();

        b.HasIndex(x => new { x.UserId, x.Email, x.Status })
            .HasDatabaseName("IX_contact_invites_UserId_Email_Status");

        b.HasIndex(x => new { x.UserId, x.PhoneNumber, x.Status })
            .HasDatabaseName("IX_contact_invites_UserId_Phone_Status");
    }
}