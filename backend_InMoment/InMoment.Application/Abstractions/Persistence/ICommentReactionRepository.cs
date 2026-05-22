using InMoment.Domain.Media;

namespace InMoment.Application.Abstractions.Persistence;

public interface ICommentReactionRepository
{
    Task<CommentReaction?> GetByCommentAndUserAsync(Guid commentId, Guid userId, CancellationToken ct);

    Task AddAsync(CommentReaction reaction, CancellationToken ct);

    Task RemoveAsync(CommentReaction reaction, CancellationToken ct);

    Task<IReadOnlyDictionary<ReactionType, int>> GetSummaryAsync(Guid commentId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<ReactionType, int>>> GetSummariesByCommentIdsAsync(
        IReadOnlyList<Guid> commentIds,
        CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, ReactionType>> GetUserReactionsByCommentIdsAsync(
        IReadOnlyList<Guid> commentIds,
        Guid userId,
        CancellationToken ct);
}