using InMoment.Application.Features.Reports.Common;
using MediatR;

namespace InMoment.Application.Features.Reports.ListMyReports;

public sealed record ListMyReportsQuery(int Limit = 50)
    : IRequest<IReadOnlyList<ReportDto>>;