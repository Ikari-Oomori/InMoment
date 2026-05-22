using InMoment.Application.Features.Reports.Common;
using MediatR;

namespace InMoment.Application.Features.Reports.GetReportDetails;

public sealed record GetReportDetailsQuery(Guid ReportId) : IRequest<ReportDetailsDto>;