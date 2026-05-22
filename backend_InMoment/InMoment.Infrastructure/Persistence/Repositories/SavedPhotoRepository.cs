using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class SavedPhotoRepository : ISavedPhotoRepository
{
    private readonly AppDbContext _db;

    public SavedPhotoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<SavedPhoto?> GetByPhotoAndUserAsync(Guid photoId, Guid userId, CancellationToken ct)
        => _db.SavedPhotos.FirstOrDefaultAsync(x => x.PhotoId == photoId && x.UserId == userId, ct);

    public Task AddAsync(SavedPhoto savedPhoto, CancellationToken ct)
        => _db.SavedPhotos.AddAsync(savedPhoto, ct).AsTask();

    public Task RemoveAsync(SavedPhoto savedPhoto, CancellationToken ct)
    {
        _db.SavedPhotos.Remove(savedPhoto);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SavedPhoto>> GetPageByUserAsync(
        Guid userId,
        int limit,
        DateTime? beforeCreatedAt,
        Guid? beforeSavedPhotoId,
        CancellationToken ct)
    {
        var query = _db.SavedPhotos
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (beforeCreatedAt.HasValue && beforeSavedPhotoId.HasValue)
        {
            var dt = beforeCreatedAt.Value;
            var id = beforeSavedPhotoId.Value;

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
}