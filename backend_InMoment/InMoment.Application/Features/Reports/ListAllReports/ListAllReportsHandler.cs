using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Reports.Common;
using MediatR;

namespace InMoment.Application.Features.Reports.ListAllReports;

public sealed class ListAllReportsHandler : IRequestHandler<ListAllReportsQuery, IReadOnlyList<ReportListItemDto>>
{
    private readonly IReportRepository _reports;
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly ReportTargetContextFactory _contextFactory;
    private readonly ReportDtoBuilders _builders;

    public ListAllReportsHandler(
        IReportRepository reports,
        ICurrentUser current,
        ISystemModeratorAccess moderatorAccess,
        ReportTargetContextFactory contextFactory,
        ReportDtoBuilders builders)
    {
        _reports = reports;
        _current = current;
        _moderatorAccess = moderatorAccess;
        _contextFactory = contextFactory;
        _builders = builders;
    }

    public async Task<IReadOnlyList<ReportListItemDto>> Handle(ListAllReportsQuery q, CancellationToken ct)
    {
        _moderatorAccess.EnsureModerator(_current.UserId);

        var limit = q.Limit is < 1 or > 200 ? 100 : q.Limit;
        var items = await _reports.GetAllFilteredAsync(
            limit,
            q.Status,
            q.TargetType,
            q.Reason,
            ct);

        var result = new List<ReportListItemDto>(items.Count);

        foreach (var report in items)
        {
            var context = await _contextFactory.BuildAsync(report.TargetType, report.TargetId, ct);
            var reporter = await _builders.BuildReporterAsync(report.ReporterUserId, ct);
            var resolution = _builders.BuildResolution(report);

            result.Add(new ReportListItemDto(
                report.Id,
                report.ReporterUserId,
                report.TargetType,
                report.TargetId,
                report.Reason,
                report.Description,
                report.Status,
                report.ReviewedByUserId,
                report.ReviewedAt,
                report.CreatedAt,
                reporter,
                context,
                resolution
            ));
        }

        return result;
    }
}