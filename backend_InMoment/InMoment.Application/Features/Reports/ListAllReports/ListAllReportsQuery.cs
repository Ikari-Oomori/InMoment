using InMoment.Application.Features.Reports.Common;
using InMoment.Domain.Reports;
using MediatR;

namespace InMoment.Application.Features.Reports.ListAllReports;

public sealed record ListAllReportsQuery(
    int Limit = 100,
    ReportStatus? Status = null,
    ReportTargetType? TargetType = null,
    ReportReason? Reason = null
) : IRequest<IReadOnlyList<ReportListItemDto>>;