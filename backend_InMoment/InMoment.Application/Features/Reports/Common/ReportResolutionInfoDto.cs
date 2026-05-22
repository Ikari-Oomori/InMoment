namespace InMoment.Application.Features.Reports.Common;

public sealed record ReportResolutionInfoDto(
    bool IsResolved,
    string? ResolutionCode,
    string ResolutionText,
    string? AppealText,
    DateTime? AppealedAt
);