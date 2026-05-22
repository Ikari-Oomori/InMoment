using InMoment.Domain.SystemMemories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class SystemMemoryConfiguration : IEntityTypeConfiguration<SystemMemory>
{
    public void Configure(EntityTypeBuilder<SystemMemory> b)
    {
        b.ToTable("system_memories");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Period).HasConversion<int>().IsRequired();

        b.Property(x => x.Title).HasMaxLength(160).IsRequired();
        b.Property(x => x.Subtitle).HasMaxLength(240).IsRequired();
        b.Property(x => x.SourcePhotoIds).HasColumnType("text").IsRequired();
        b.Property(x => x.PreviewPhotoId);

        b.Property(x => x.GeneratedVideoStorageKey).HasMaxLength(512);
        b.Property(x => x.GeneratedVideoContentType).HasMaxLength(80);
        b.Property(x => x.GeneratedVideoSizeBytes);

        b.Property(x => x.PeriodStartedAtUtc).IsRequired();
        b.Property(x => x.PeriodEndedAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.ViewedAtUtc);

        b.HasIndex(x => new { x.UserId, x.Period, x.PeriodEndedAtUtc }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}
