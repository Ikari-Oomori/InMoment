using InMoment.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");

        b.HasKey(x => x.Id);

        b.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        b.HasIndex(x => x.Email).IsUnique();

        b.Property(x => x.UserName)
            .IsRequired()
            .HasMaxLength(64);

        b.HasIndex(x => x.UserName).IsUnique();

        b.Property(x => x.PasswordHash)
            .IsRequired();

        b.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        b.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        b.Property(x => x.PhoneNumber)
            .HasMaxLength(32);

        b.HasIndex(x => x.PhoneNumber)
            .IsUnique()
            .HasFilter("\"PhoneNumber\" IS NOT NULL");

        b.Property(x => x.ProfilePhotoUrl)
            .HasMaxLength(1000);

        b.Property(x => x.ActiveGroupId);

        b.Property(x => x.CreatedAt)
            .IsRequired();

        b.Property(x => x.IsActive)
            .IsRequired();

        b.Property(x => x.IsOnboardingCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        b.Property(x => x.OnboardingCompletedAt);

        b.Property(x => x.HasCompletedContactsStep)
            .IsRequired()
            .HasDefaultValue(false);

        b.Property(x => x.SkippedContactsImport)
            .IsRequired()
            .HasDefaultValue(false);

        b.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_users_IsActive");

        b.Property(x => x.DeletedEmail)
            .HasMaxLength(256);

        b.Property(x => x.DeletedUserName)
            .HasMaxLength(64);

        b.HasIndex(x => x.DeletedEmail)
            .HasDatabaseName("IX_users_DeletedEmail");

        b.HasIndex(x => x.DeletedUserName)
            .HasDatabaseName("IX_users_DeletedUserName");
    }
}