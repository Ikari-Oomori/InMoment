using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> b)
    {
        b.ToTable("photos");
        b.HasKey(x => x.Id);

        b.Property(x => x.GroupId).IsRequired();
        b.Property(x => x.UploadedByUserId).IsRequired();

        b.Property(x => x.StorageKey).IsRequired().HasMaxLength(512);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        b.Property(x => x.SizeBytes).IsRequired();
        b.Property(x => x.Caption).HasMaxLength(500);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.IsDeleted).IsRequired();

        b.HasIndex(x => new { x.GroupId, x.CreatedAt, x.Id })
            .HasDatabaseName("IX_photos_GroupId_CreatedAt_Id");

        b.HasIndex(x => x.UploadedByUserId);
    }
}