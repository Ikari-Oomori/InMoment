using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Reports.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Reports.GetReportDetails;

public sealed class GetReportDetailsHandler : IRequestHandler<GetReportDetailsQuery, ReportDetailsDto>
{
    private readonly IReportRepository _reports;
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly ReportTargetContextFactory _contextFactory;
    private readonly ReportDtoBuilders _builders;

    public GetReportDetailsHandler(
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

    public async Task<ReportDetailsDto> Handle(GetReportDetailsQuery query, CancellationToken ct)
    {
        _moderatorAccess.EnsureModerator(_current.UserId);

        if (query.ReportId == Guid.Empty)
            throw new ValidationException("ReportId is required.");

        var report = await _reports.GetByIdAsync(query.ReportId, ct)
                     ?? throw new NotFoundException("Report not found.");

        var context = await _contextFactory.BuildAsync(report.TargetType, report.TargetId, ct);
        var reporter = await _builders.BuildReporterAsync(report.ReporterUserId, ct);
        var resolution = _builders.BuildResolution(report);

        return new ReportDetailsDto(
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
        );
    }
}