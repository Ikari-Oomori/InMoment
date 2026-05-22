using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class ReactionRepository : IReactionRepository
{
    private readonly AppDbContext _db;

    public ReactionRepository(AppDbContext db) => _db = db;

    public Task<Reaction?> GetByPhotoAndUserAsync(Guid photoId, Guid userId, CancellationToken ct)
        => _db.Reactions.FirstOrDefaultAsync(x => x.PhotoId == photoId && x.UserId == userId, ct);

    public Task AddAsync(Reaction reaction, CancellationToken ct)
        => _db.Reactions.AddAsync(reaction, ct).AsTask();

    public Task RemoveAsync(Reaction reaction, CancellationToken ct)
    {
        _db.Reactions.Remove(reaction);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<ReactionType, int>> GetSummaryAsync(Guid photoId, CancellationToken ct)
    {
        var data = await _db.Reactions
            .AsNoTracking()
            .Where(x => x.PhotoId == photoId)
            .GroupBy(x => x.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return data.ToDictionary(x => x.Type, x => x.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<ReactionType, int>>> GetSummariesByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct)
    {
        if (photoIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>();

        var rows = await _db.Reactions
            .AsNoTracking()
            .Where(x => photoIds.Contains(x.PhotoId))
            .GroupBy(x => new { x.PhotoId, x.Type })
            .Select(g => new { g.Key.PhotoId, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>();

        foreach (var grp in rows.GroupBy(r => r.PhotoId))
        {
            result[grp.Key] = grp.ToDictionary(x => x.Type, x => x.Count);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, ReactionType>> GetUserReactionsByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        Guid userId,
        CancellationToken ct)
    {
        if (photoIds.Count == 0)
            return new Dictionary<Guid, ReactionType>();

        var rows = await _db.Reactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && photoIds.Contains(x.PhotoId))
            .Select(x => new { x.PhotoId, x.Type })
            .ToListAsync(ct);

        // гарантируем 1 реакцию на фото (у тебя unique (PhotoId,UserId))
        return rows.ToDictionary(x => x.PhotoId, x => x.Type);
    }
}