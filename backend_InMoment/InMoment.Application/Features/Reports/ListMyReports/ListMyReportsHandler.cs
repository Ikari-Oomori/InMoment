using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Reports.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Reports.ListMyReports;

public sealed class ListMyReportsHandler : IRequestHandler<ListMyReportsQuery, IReadOnlyList<ReportDto>>
{
    private readonly IReportRepository _reports;
    private readonly ICurrentUser _current;
    private readonly ReportDtoBuilders _builders;
    private readonly ReportTargetContextFactory _targetContextFactory;

    public ListMyReportsHandler(
        IReportRepository reports,
        ICurrentUser current,
        ReportDtoBuilders builders,
        ReportTargetContextFactory targetContextFactory)
    {
        _reports = reports;
        _current = current;
        _builders = builders;
        _targetContextFactory = targetContextFactory;
    }

    public async Task<IReadOnlyList<ReportDto>> Handle(ListMyReportsQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var limit = q.Limit is < 1 or > 100 ? 50 : q.Limit;
        var items = await _reports.GetByReporterAsync(_current.UserId, limit, ct);

        var result = new List<ReportDto>(items.Count);

        foreach (var x in items)
        {
            var reporter = await _builders.BuildReporterAsync(x.ReporterUserId, ct);
            var targetContext = await _targetContextFactory.BuildAsync(x.TargetType, x.TargetId, ct);

            result.Add(new ReportDto(
                x.Id,
                x.ReporterUserId,
                x.TargetType,
                x.TargetId,
                x.Reason,
                x.Description,
                x.Status,
                x.ReviewedByUserId,
                x.ReviewedAt,
                x.CreatedAt,
                reporter,
                targetContext,
                _builders.BuildResolution(x)
            ));
        }

        return result;
    }
}