using InMoment.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("reports");
        b.HasKey(x => x.Id);

        b.Property(x => x.ReporterUserId).IsRequired();
        b.Property(x => x.TargetType).HasConversion<int>().IsRequired();
        b.Property(x => x.TargetId).IsRequired();
        b.Property(x => x.Reason).HasConversion<int>().IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.DecisionAction).HasConversion<int?>();

        b.Property(x => x.ReviewedByUserId);
        b.Property(x => x.ReviewedAt);

        b.Property(x => x.AppealText).HasMaxLength(1000);
        b.Property(x => x.AppealedAt);
        b.Property(x => x.AppealedByUserId);

        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => new { x.ReporterUserId, x.TargetType, x.TargetId, x.Status })
            .HasDatabaseName("IX_reports_Reporter_Target_Status");

        b.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_reports_Status_CreatedAt");
    }
}