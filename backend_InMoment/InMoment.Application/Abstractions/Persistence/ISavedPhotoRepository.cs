using InMoment.Domain.Media;

namespace InMoment.Application.Abstractions.Persistence;

public interface ISavedPhotoRepository
{
    Task<SavedPhoto?> GetByPhotoAndUserAsync(Guid photoId, Guid userId, CancellationToken ct);

    Task AddAsync(SavedPhoto savedPhoto, CancellationToken ct);

    Task RemoveAsync(SavedPhoto savedPhoto, CancellationToken ct);

    Task<IReadOnlyList<SavedPhoto>> GetPageByUserAsync(
        Guid userId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforeSavedPhotoId,
        CancellationToken ct);
}