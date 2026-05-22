using InMoment.Domain.Media;

namespace InMoment.Application.Abstractions.Persistence;

public interface IPhotoRepository
{
    Task AddAsync(Photo photo, CancellationToken ct);

    Task<Photo?> GetByIdAsync(Guid photoId, CancellationToken ct);

    Task<IReadOnlyList<Photo>> GetFeedByGroupAsync(
        Guid groupId,
        int limit,
        CancellationToken ct);

    Task<IReadOnlyList<Photo>> GetPageByGroupAsync(
        Guid groupId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforePhotoId,
        CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, Photo>> GetByIdsAsync(
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct);

    Task<Photo?> GetLatestByGroupAsync(
        Guid groupId,
        CancellationToken ct);

    Task<IReadOnlyList<Photo>> GetByGroupAndDateRangeAsync(
        Guid groupId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct);

    Task<IReadOnlyList<DateOnly>> GetPostingDatesByGroupAsync(
        Guid groupId,
        CancellationToken ct);

    Task<int> CountByGroupAsync(
        Guid groupId,
        CancellationToken ct);

    Task<IReadOnlyList<Photo>> GetByUserAndDateRangeAsync(
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct);

    Task<IReadOnlyList<DateOnly>> GetPostingDatesByUserAsync(
        Guid userId,
        CancellationToken ct);

    Task<int> CountByUserAsync(
        Guid userId,
        CancellationToken ct);
}