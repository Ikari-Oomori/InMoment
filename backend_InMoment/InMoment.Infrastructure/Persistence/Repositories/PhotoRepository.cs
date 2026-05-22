using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class PhotoRepository : IPhotoRepository
{
    private readonly AppDbContext _db;

    public PhotoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Photo photo, CancellationToken ct)
        => _db.Photos.AddAsync(photo, ct).AsTask();

    public Task<Photo?> GetByIdAsync(Guid photoId, CancellationToken ct)
        => _db.Photos.FirstOrDefaultAsync(x => x.Id == photoId, ct);

    public async Task<IReadOnlyList<Photo>> GetFeedByGroupAsync(
        Guid groupId,
        int limit,
        CancellationToken ct)
    {
        return await _db.Photos
            .AsNoTracking()
            .Where(x => x.GroupId == groupId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Photo>> GetPageByGroupAsync(
        Guid groupId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforePhotoId,
        CancellationToken ct)
    {
        var query = _db.Photos
            .AsNoTracking()
            .Where(x => x.GroupId == groupId && !x.IsDeleted);

        if (beforeCreatedAt.HasValue && beforePhotoId.HasValue)
        {
            var dt = beforeCreatedAt.Value;
            var id = beforePhotoId.Value;

            query = query.Where(x =>
                x.CreatedAt < dt ||
                (x.CreatedAt == dt && x.Id.CompareTo(id) < 0));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Photo>> GetByIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct)
    {
        if (photoIds.Count == 0)
            return new Dictionary<Guid, Photo>();

        var items = await _db.Photos
            .AsNoTracking()
            .Where(x => photoIds.Contains(x.Id))
            .ToListAsync(ct);

        return items.ToDictionary(x => x.Id, x => x);
    }

    public Task<Photo?> GetLatestByGroupAsync(
        Guid groupId,
        CancellationToken ct)
    {
        return _db.Photos
            .AsNoTracking()
            .Where(x => x.GroupId == groupId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Photo>> GetByGroupAndDateRangeAsync(
        Guid groupId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct)
    {
        return await _db.Photos
            .AsNoTracking()
            .Where(x =>
                x.GroupId == groupId &&
                !x.IsDeleted &&
                x.CreatedAt >= fromUtc &&
                x.CreatedAt < toUtc)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DateOnly>> GetPostingDatesByGroupAsync(
        Guid groupId,
        CancellationToken ct)
    {
        var timestamps = await _db.Photos
            .AsNoTracking()
            .Where(x => x.GroupId == groupId && !x.IsDeleted)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);

        return timestamps
            .Select(x => DateOnly.FromDateTime(x.Date))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public Task<int> CountByGroupAsync(
        Guid groupId,
        CancellationToken ct)
    {
        return _db.Photos
            .AsNoTracking()
            .CountAsync(x => x.GroupId == groupId && !x.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<Photo>> GetByUserAndDateRangeAsync(
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct)
    {
        return await _db.Photos
            .AsNoTracking()
            .Where(x =>
                x.UploadedByUserId == userId &&
                !x.IsDeleted &&
                x.CreatedAt >= fromUtc &&
                x.CreatedAt < toUtc)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DateOnly>> GetPostingDatesByUserAsync(
        Guid userId,
        CancellationToken ct)
    {
        var timestamps = await _db.Photos
            .AsNoTracking()
            .Where(x => x.UploadedByUserId == userId && !x.IsDeleted)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);

        return timestamps
            .Select(x => DateOnly.FromDateTime(x.Date))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public Task<int> CountByUserAsync(
        Guid userId,
        CancellationToken ct)
    {
        return _db.Photos
            .AsNoTracking()
            .CountAsync(x => x.UploadedByUserId == userId && !x.IsDeleted, ct);
    }
}