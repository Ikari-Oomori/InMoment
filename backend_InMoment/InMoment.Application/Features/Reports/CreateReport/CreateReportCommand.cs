using InMoment.Domain.Reports;
using MediatR;

namespace InMoment.Application.Features.Reports.CreateReport;

public sealed record CreateReportCommand(
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Description
) : IRequest<Guid>;