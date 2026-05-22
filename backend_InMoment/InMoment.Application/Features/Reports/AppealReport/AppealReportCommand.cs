using MediatR;

namespace InMoment.Application.Features.Reports.AppealReport;

public sealed record AppealReportCommand(
    Guid ReportId,
    string Text
) : IRequest<Guid>;