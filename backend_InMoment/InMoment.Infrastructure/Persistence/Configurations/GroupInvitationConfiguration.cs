using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class GroupInvitationConfiguration : IEntityTypeConfiguration<GroupInvitation>
{
    public void Configure(EntityTypeBuilder<GroupInvitation> b)
    {
        b.ToTable("group_invitations");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        b.Property(x => x.GroupId)
            .HasColumnName("GroupId")
            .IsRequired();

        b.Property(x => x.InvitedUserId)
            .HasColumnName("InvitedUserId")
            .IsRequired();

        b.Property(x => x.InvitedByUserId)
            .HasColumnName("InvitedByUserId")
            .IsRequired();

        b.Property(x => x.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        b.Property(x => x.RespondedAt)
            .HasColumnName("RespondedAt");

         b.HasIndex(x => new { x.GroupId, x.InvitedUserId })
            .HasDatabaseName("IX_group_invitations_GroupId_InvitedUserId_PendingUnique")
            .IsUnique()
            .HasFilter("\"Status\" = 1");

        b.HasIndex(x => new { x.InvitedUserId, x.Status })
            .HasDatabaseName("IX_group_invitations_InvitedUserId_Status");
    }
}