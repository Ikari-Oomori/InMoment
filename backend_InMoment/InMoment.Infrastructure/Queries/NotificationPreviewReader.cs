using InMoment.Application.Abstractions.Queries;
using InMoment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Queries;

public sealed class NotificationPreviewReader : INotificationPreviewReader
{
    private readonly AppDbContext _db;

    public NotificationPreviewReader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationPreviewBundle> GetBundleAsync(
        IReadOnlyList<Guid> actorUserIds,
        IReadOnlyList<Guid> groupIds,
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct)
    {
        var distinctActorIds = actorUserIds.Where(x => x != Guid.Empty).Distinct().ToList();
        var distinctGroupIds = groupIds.Where(x => x != Guid.Empty).Distinct().ToList();
        var distinctPhotoIds = photoIds.Where(x => x != Guid.Empty).Distinct().ToList();

        var actors = await _db.Users
            .AsNoTracking()
            .Where(x => distinctActorIds.Contains(x.Id))
            .Select(x => new NotificationActorPreview(
                x.Id,
                BuildDisplayName(x.UserName, x.FirstName, x.LastName),
                x.UserName,
                x.ProfilePhotoUrl
            ))
            .ToListAsync(ct);

        var groups = await _db.Groups
            .AsNoTracking()
            .Where(x => distinctGroupIds.Contains(x.Id))
            .Select(x => new NotificationGroupPreview(
                x.Id,
                x.Name,
                x.AvatarUrl
            ))
            .ToListAsync(ct);

        var photos = await _db.Photos
            .AsNoTracking()
            .Where(x => distinctPhotoIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new NotificationPhotoPreview(
                x.Id,
                x.StorageKey,
                x.Caption
            ))
            .ToListAsync(ct);

        return new NotificationPreviewBundle(
            Actors: actors.ToDictionary(x => x.UserId, x => x),
            Groups: groups.ToDictionary(x => x.GroupId, x => x),
            Photos: photos.ToDictionary(x => x.PhotoId, x => x)
        );
    }

    private static string BuildDisplayName(string userName, string firstName, string lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName;

        return userName;
    }
}