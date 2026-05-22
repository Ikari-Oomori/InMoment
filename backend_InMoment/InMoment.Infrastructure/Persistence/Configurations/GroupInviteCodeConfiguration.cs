using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class GroupInviteCodeConfiguration : IEntityTypeConfiguration<GroupInviteCode>
{
    public void Configure(EntityTypeBuilder<GroupInviteCode> b)
    {
        b.ToTable("group_invite_codes");

        b.HasKey(x => x.Id);

        b.Property(x => x.GroupId)
            .IsRequired();

        b.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(32);

        b.HasIndex(x => x.Code)
            .IsUnique();

        b.Property(x => x.CreatedByUserId)
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.ExpiresAtUtc);

        b.Property(x => x.MaxUses);

        b.Property(x => x.UsesCount)
            .IsRequired();

        b.Property(x => x.IsRevoked)
            .IsRequired();

        b.HasIndex(x => x.GroupId);

        b.HasOne<Group>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}