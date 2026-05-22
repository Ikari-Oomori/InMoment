using InMoment.Domain.Media;

namespace InMoment.Application.Abstractions.Persistence;

public interface ICommentRepository
{
    Task AddAsync(Comment comment, CancellationToken ct);

    Task<Comment?> GetByIdAsync(Guid commentId, CancellationToken ct);

    Task<IReadOnlyList<Comment>> GetByPhotoAsync(Guid photoId, int limit, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, int>> GetCountsByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct);

    Task<IReadOnlyList<Comment>> GetPageByPhotoAsync(
        Guid photoId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforeCommentId,
        CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, Comment>> GetLatestByPhotoIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct);
}