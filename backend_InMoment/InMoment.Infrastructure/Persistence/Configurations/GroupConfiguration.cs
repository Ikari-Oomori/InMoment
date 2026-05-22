using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.ToTable("groups");
        b.HasKey(x => x.Id);

        b.Property(x => x.OwnerId).IsRequired();
        b.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        b.Property(x => x.Description)
            .HasMaxLength(500);

        b.Property(x => x.AvatarUrl)
            .HasMaxLength(1000);

        b.Property(x => x.CreatedBy).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.IsActive).IsRequired();

        b.Metadata.FindNavigation(nameof(Group.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}