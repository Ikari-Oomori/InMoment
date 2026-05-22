using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> b)
    {
        b.ToTable("comment_reactions");
        b.HasKey(x => x.Id);

        b.Property(x => x.CommentId).IsRequired();
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Type).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        b.HasIndex(x => new { x.CommentId, x.UserId }).IsUnique();
        b.HasIndex(x => x.CommentId);
    }
}