using InMoment.Domain.Reports;

namespace InMoment.Application.Features.Reports.Common;

public sealed record ReporterPreviewDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl
);

public sealed record ReportPhotoPreviewDto(
    Guid PhotoId,
    Guid GroupId,
    Guid AuthorUserId,
    string AuthorUserName,
    string AuthorDisplayName,
    string? AuthorProfilePhotoUrl,
    string? GroupName,
    string? PhotoUrl,
    string? Caption,
    DateTime CreatedAt,
    bool IsDeleted
);

public sealed record ReportCommentPreviewDto(
    Guid CommentId,
    Guid PhotoId,
    Guid AuthorUserId,
    string AuthorUserName,
    string AuthorDisplayName,
    string? AuthorProfilePhotoUrl,
    string Text,
    DateTime CreatedAt,
    bool IsDeleted,
    Guid? ParentCommentId,
    string? ParentCommentTextPreview
);

public sealed record ReportUserPreviewDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl,
    bool IsActive,
    DateTime CreatedAt,
    int ReportsAgainstCount,
    int PendingReportsAgainstCount,
    int ResolvedReportsAgainstCount
);

public sealed record ReportTargetContextDto(
    ReportPhotoPreviewDto? Photo,
    ReportCommentPreviewDto? Comment,
    ReportUserPreviewDto? User
);

public sealed record ReportListItemDto(
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

public sealed record ReportDetailsDto(
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