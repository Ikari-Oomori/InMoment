using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class CommentReactionRepository : ICommentReactionRepository
{
    private readonly AppDbContext _db;

    public CommentReactionRepository(AppDbContext db) => _db = db;

    public Task<CommentReaction?> GetByCommentAndUserAsync(Guid commentId, Guid userId, CancellationToken ct)
        => _db.CommentReactions.FirstOrDefaultAsync(
            x => x.CommentId == commentId && x.UserId == userId,
            ct);

    public Task AddAsync(CommentReaction reaction, CancellationToken ct)
        => _db.CommentReactions.AddAsync(reaction, ct).AsTask();

    public Task RemoveAsync(CommentReaction reaction, CancellationToken ct)
    {
        _db.CommentReactions.Remove(reaction);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<ReactionType, int>> GetSummaryAsync(Guid commentId, CancellationToken ct)
    {
        var data = await _db.CommentReactions
            .AsNoTracking()
            .Where(x => x.CommentId == commentId)
            .GroupBy(x => x.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return data.ToDictionary(x => x.Type, x => x.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<ReactionType, int>>> GetSummariesByCommentIdsAsync(
        IReadOnlyList<Guid> commentIds,
        CancellationToken ct)
    {
        if (commentIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>();

        var rows = await _db.CommentReactions
            .AsNoTracking()
            .Where(x => commentIds.Contains(x.CommentId))
            .GroupBy(x => new { x.CommentId, x.Type })
            .Select(g => new { g.Key.CommentId, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>();

        foreach (var grp in rows.GroupBy(x => x.CommentId))
        {
            result[grp.Key] = grp.ToDictionary(x => x.Type, x => x.Count);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, ReactionType>> GetUserReactionsByCommentIdsAsync(
        IReadOnlyList<Guid> commentIds,
        Guid userId,
        CancellationToken ct)
    {
        if (commentIds.Count == 0)
            return new Dictionary<Guid, ReactionType>();

        var rows = await _db.CommentReactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && commentIds.Contains(x.CommentId))
            .Select(x => new { x.CommentId, x.Type })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.CommentId, x => x.Type);
    }
}