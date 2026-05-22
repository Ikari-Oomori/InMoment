using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class CommentRepository : ICommentRepository
{
    private readonly AppDbContext _db;

    public CommentRepository(AppDbContext db) => _db = db;

    public Task AddAsync(Comment comment, CancellationToken ct)
        => _db.Comments.AddAsync(comment, ct).AsTask();

    public Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken ct)
        => _db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, ct);

    public async Task<IReadOnlyList<Comment>> GetByPhotoAsync(Guid photoId, int limit, CancellationToken ct)
    {
        return await _db.Comments
            .AsNoTracking()
            .Where(x => x.PhotoId == photoId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetCountsByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct)
    {
        if (photoIds.Count == 0)
            return new Dictionary<Guid, int>();

        var rows = await _db.Comments
            .AsNoTracking()
            .Where(x => !x.IsDeleted && photoIds.Contains(x.PhotoId))
            .GroupBy(x => x.PhotoId)
            .Select(g => new { PhotoId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.PhotoId, x => x.Count);
    }

    public async Task<IReadOnlyList<Comment>> GetPageByPhotoAsync(
        Guid photoId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforeCommentId,
        CancellationToken ct)
    {
        var query = _db.Comments
            .AsNoTracking()
            .Where(x => x.PhotoId == photoId && !x.IsDeleted);

        if (beforeCreatedAt.HasValue && beforeCommentId.HasValue)
        {
            var dt = beforeCreatedAt.Value;
            var id = beforeCommentId.Value;

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

    public async Task<IReadOnlyDictionary<Guid, Comment>> GetLatestByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct)
    {
        if (photoIds.Count == 0)
            return new Dictionary<Guid, Comment>();

        var comments = await _db.Comments
            .AsNoTracking()
            .Where(x => !x.IsDeleted && photoIds.Contains(x.PhotoId))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var result = new Dictionary<Guid, Comment>();

        foreach (var comment in comments)
        {
            if (!result.ContainsKey(comment.PhotoId))
                result[comment.PhotoId] = comment;
        }

        return result;
    }
}