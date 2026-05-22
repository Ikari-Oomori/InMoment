using InMoment.Domain.Reports;

namespace InMoment.Application.Features.Reports.Common;

public sealed record ReportDto(
    Guid Id,
    Guid ReporterUserId,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Description,
    ReportStatus Status,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    ReporterPreviewDto? Reporter,
    ReportTargetContextDto TargetContext,
    ReportResolutionInfoDto Resolution
);