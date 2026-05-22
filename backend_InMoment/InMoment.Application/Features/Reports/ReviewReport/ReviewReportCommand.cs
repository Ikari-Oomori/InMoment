using InMoment.Domain.Reports;
using MediatR;

namespace InMoment.Application.Features.Reports.ReviewReport;

public sealed record ReviewReportCommand(
    Guid ReportId,
    ReportStatus Status,
    ReviewReportAction Action = ReviewReportAction.None
) : IRequest<Guid>;