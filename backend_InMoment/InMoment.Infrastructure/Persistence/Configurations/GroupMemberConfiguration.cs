using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> b)
    {
        b.ToTable("group_members");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        b.Property(x => x.GroupId)
            .HasColumnName("GroupId")
            .IsRequired();

        b.Property(x => x.UserId)
            .HasColumnName("UserId")
            .IsRequired();

        b.Property(x => x.Role)
            .HasColumnName("Role")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.IsActive)
            .HasColumnName("IsActive")
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        b.HasIndex(x => new { x.GroupId, x.UserId })
            .HasDatabaseName("IX_group_members_GroupId_UserId_ActiveUnique")
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        b.HasIndex(x => x.GroupId)
            .HasDatabaseName("IX_group_members_GroupId_Owner_ActiveUnique")
            .IsUnique()
            .HasFilter("\"IsActive\" = true AND \"Role\" = 1");

        b.HasIndex(x => new { x.GroupId, x.IsActive })
            .HasDatabaseName("IX_group_members_GroupId_IsActive");

        b.HasIndex(x => new { x.UserId, x.IsActive })
            .HasDatabaseName("IX_group_members_UserId_IsActive");
    }
}