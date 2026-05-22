using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class SavedPhotoConfiguration : IEntityTypeConfiguration<SavedPhoto>
{
    public void Configure(EntityTypeBuilder<SavedPhoto> b)
    {
        b.ToTable("saved_photos");

        b.HasKey(x => x.Id);

        b.Property(x => x.PhotoId).IsRequired();
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => new { x.PhotoId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id })
            .HasDatabaseName("IX_saved_photos_UserId_CreatedAt_Id");
        b.HasIndex(x => x.PhotoId);
    }
}