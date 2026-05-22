using InMoment.Domain.Reports;

namespace InMoment.Application.Abstractions.Persistence;

public interface IReportRepository
{
    Task AddAsync(Report report, CancellationToken ct);

    Task<Report?> GetByIdAsync(Guid reportId, CancellationToken ct);

    Task<IReadOnlyList<Report>> GetByReporterAsync(Guid reporterUserId, int limit, CancellationToken ct);

    Task<IReadOnlyList<Report>> GetAllAsync(int limit, CancellationToken ct);

    Task<bool> ExistsSimilarPendingAsync(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken ct);

    Task<int> CountByTargetAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken ct);

    Task<int> CountByTargetAndStatusAsync(
        ReportTargetType targetType,
        Guid targetId,
        ReportStatus status,
        CancellationToken ct);

    Task<IReadOnlyList<Report>> GetAllFilteredAsync(
        int limit,
        ReportStatus? status,
        ReportTargetType? targetType,
        ReportReason? reason,
        CancellationToken ct);
}