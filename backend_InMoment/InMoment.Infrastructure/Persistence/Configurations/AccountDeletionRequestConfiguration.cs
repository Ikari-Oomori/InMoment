using InMoment.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class AccountDeletionRequestConfiguration : IEntityTypeConfiguration<AccountDeletionRequest>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRequest> b)
    {
        b.ToTable("account_deletion_requests");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.RequestedEmail)
            .IsRequired()
            .HasMaxLength(256);

        b.Property(x => x.RequestedUserName)
            .IsRequired()
            .HasMaxLength(64);

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Note)
            .HasMaxLength(2000);

        b.Property(x => x.ProcessingNote)
            .HasMaxLength(2000);

        b.Property(x => x.RequestedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
        b.Property(x => x.ProcessedAtUtc);
        b.Property(x => x.ProcessedByUserId);

        b.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_account_deletion_requests_UserId");

        b.HasIndex(x => new { x.UserId, x.RequestedAtUtc })
            .HasDatabaseName("IX_account_deletion_requests_UserId_RequestedAtUtc");

        b.HasIndex(x => new { x.UserId, x.Status })
            .HasDatabaseName("IX_account_deletion_requests_UserId_Status");

        b.HasIndex(x => new { x.Status, x.RequestedAtUtc })
            .HasDatabaseName("IX_account_deletion_requests_Status_RequestedAtUtc");

        b.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_account_deletion_requests_UserId_ActiveUnique")
            .IsUnique()
            .HasFilter("\"Status\" IN (1, 2)");
    }
}