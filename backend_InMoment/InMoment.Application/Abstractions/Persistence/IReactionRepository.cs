using InMoment.Domain.Media;

namespace InMoment.Application.Abstractions.Persistence;

public interface IReactionRepository
{
    Task<Reaction?> GetByPhotoAndUserAsync(Guid photoId, Guid userId, CancellationToken ct);
    Task AddAsync(Reaction reaction, CancellationToken ct);
    Task RemoveAsync(Reaction reaction, CancellationToken ct);
    Task<IReadOnlyDictionary<ReactionType, int>> GetSummaryAsync(Guid photoId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<ReactionType, int>>> GetSummariesByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, ReactionType>> GetUserReactionsByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        Guid userId,
        CancellationToken ct);
}