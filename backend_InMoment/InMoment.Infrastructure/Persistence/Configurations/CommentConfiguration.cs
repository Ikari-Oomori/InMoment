using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("comments");
        b.HasKey(x => x.Id);

        b.Property(x => x.PhotoId).IsRequired();
        b.Property(x => x.UserId).IsRequired();

        b.Property(x => x.ParentCommentId).IsRequired(false);

        b.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(500);

        b.Property(x => x.GifUrl)
            .IsRequired(false)
            .HasMaxLength(2048);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.IsDeleted).IsRequired();

        b.HasOne<Comment>()
            .WithMany()
            .HasForeignKey(x => x.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.PhotoId, x.CreatedAt, x.Id })
            .HasDatabaseName("IX_comments_PhotoId_CreatedAt_Id");

        b.HasIndex(x => x.ParentCommentId)
            .HasDatabaseName("IX_comments_ParentCommentId");
    }
}