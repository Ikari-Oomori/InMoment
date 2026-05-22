using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> b)
    {
        b.ToTable("reactions");
        b.HasKey(x => x.Id);

        b.Property(x => x.PhotoId).IsRequired();
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Type).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        b.HasIndex(x => new { x.PhotoId, x.UserId }).IsUnique();

        b.HasIndex(x => x.PhotoId);
    }
}