using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;

    public ReportRepository(AppDbContext db) => _db = db;

    public Task AddAsync(Report report, CancellationToken ct)
        => _db.Set<Report>().AddAsync(report, ct).AsTask();

    public Task<Report?> GetByIdAsync(Guid reportId, CancellationToken ct)
        => _db.Set<Report>().FirstOrDefaultAsync(x => x.Id == reportId, ct);

    public async Task<IReadOnlyList<Report>> GetByReporterAsync(Guid reporterUserId, int limit, CancellationToken ct)
    {
        return await _db.Set<Report>()
            .AsNoTracking()
            .Where(x => x.ReporterUserId == reporterUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Report>> GetAllAsync(int limit, CancellationToken ct)
    {
        return await _db.Set<Report>()
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsSimilarPendingAsync(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken ct)
    {
        return _db.Set<Report>().AnyAsync(x =>
            x.ReporterUserId == reporterUserId &&
            x.TargetType == targetType &&
            x.TargetId == targetId &&
            x.Status == ReportStatus.Pending, ct);
    }

    public async Task<int> CountByTargetAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken ct)
    {
        return await _db.Reports
            .CountAsync(x => x.TargetType == targetType && x.TargetId == targetId, ct);
    }

    public async Task<int> CountByTargetAndStatusAsync(
        ReportTargetType targetType,
        Guid targetId,
        ReportStatus status,
        CancellationToken ct)
    {
        return await _db.Reports
            .CountAsync(x =>
                x.TargetType == targetType &&
                x.TargetId == targetId &&
                x.Status == status,
                ct);
    }

    public async Task<IReadOnlyList<Report>> GetAllFilteredAsync(
        int limit,
        ReportStatus? status,
        ReportTargetType? targetType,
        ReportReason? reason,
        CancellationToken ct)
    {
        var query = _db.Reports.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (targetType.HasValue)
            query = query.Where(x => x.TargetType == targetType.Value);

        if (reason.HasValue)
            query = query.Where(x => x.Reason == reason.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}