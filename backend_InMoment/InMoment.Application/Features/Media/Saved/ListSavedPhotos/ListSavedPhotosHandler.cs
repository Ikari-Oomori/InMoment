using System.Globalization;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Media.Saved.ListSavedPhotos;

public sealed class ListSavedPhotosHandler : IRequestHandler<ListSavedPhotosQuery, SavedPhotosPageDto>
{
    private readonly ICurrentUser _current;
    private readonly ISavedPhotoRepository _saved;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly IBlockedUserRepository _blocks;
    private readonly IFileStorage _storage;

    public ListSavedPhotosHandler(
        ICurrentUser current,
        ISavedPhotoRepository saved,
        IPhotoRepository photos,
        IGroupRepository groups,
        IUserRepository users,
        IBlockedUserRepository blocks,
        IFileStorage storage)
    {
        _current = current;
        _saved = saved;
        _photos = photos;
        _groups = groups;
        _users = users;
        _blocks = blocks;
        _storage = storage;
    }

    public async Task<SavedPhotosPageDto> Handle(ListSavedPhotosQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var limit = q.Limit is < 1 or > 50 ? 20 : q.Limit;

        DateTime? beforeCreatedAt = null;
        Guid? beforeSavedId = null;

        if (!string.IsNullOrWhiteSpace(q.Cursor))
        {
            if (!TryParseCursor(q.Cursor!, out beforeCreatedAt, out beforeSavedId))
                throw new ValidationException("Invalid cursor format.");
        }

        var rawItems = await _saved.GetPageByUserAsync(
            _current.UserId,
            limit * 2,
            beforeCreatedAt,
            beforeSavedId,
            ct);

        if (rawItems.Count == 0)
            return new SavedPhotosPageDto(Array.Empty<SavedPhotoItemDto>(), null);

        var photoIds = rawItems
            .Select(x => x.PhotoId)
            .Distinct()
            .ToList();

        var photosMap = await _photos.GetByIdsAsync(photoIds, ct);

        var visible = new List<(Guid SavedId, DateTime SavedAt, Domain.Media.Photo Photo)>(limit);

        foreach (var row in rawItems)
        {
            if (!photosMap.TryGetValue(row.PhotoId, out var photo))
                continue;

            if (photo.IsDeleted)
                continue;

            var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
            if (!isMember)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
                continue;

            visible.Add((row.Id, row.CreatedAt, photo));

            if (visible.Count == limit)
                break;
        }

        if (visible.Count == 0)
            return new SavedPhotosPageDto(Array.Empty<SavedPhotoItemDto>(), null);

        var authorIds = visible
            .Select(x => x.Photo.UploadedByUserId)
            .Distinct()
            .ToList();

        var authors = await _users.GetByIdsAsync(authorIds, ct)
                      ?? Array.Empty<User>();

        var authorMap = authors.ToDictionary(x => x.Id, x => x);

        var groupIds = visible
            .Select(x => x.Photo.GroupId)
            .Distinct()
            .ToList();

        var groupMap = new Dictionary<Guid, Group>();

        foreach (var groupId in groupIds)
        {
            var group = await _groups.GetByIdAsync(groupId, ct);
            if (group is not null)
                groupMap[groupId] = group;
        }

        var dto = visible
            .Where(x => groupMap.ContainsKey(x.Photo.GroupId))
            .Select(x =>
            {
                groupMap.TryGetValue(x.Photo.GroupId, out var group);
                authorMap.TryGetValue(x.Photo.UploadedByUserId, out var author);

                return new SavedPhotoItemDto(
                    PhotoId: x.Photo.Id,
                    GroupId: x.Photo.GroupId,
                    GroupName: group?.Name ?? string.Empty,
                    GroupAvatarUrl: group?.AvatarUrl,
                    UploadedByUserId: x.Photo.UploadedByUserId,
                    UploadedByUserName: author?.UserName ?? string.Empty,
                    UploadedByUserProfilePhotoUrl: author?.ProfilePhotoUrl,
                    IsMine: x.Photo.UploadedByUserId == _current.UserId,
                    PhotoUrl: _storage.GetPublicUrl(x.Photo.StorageKey),
                    ContentType: x.Photo.ContentType,
                    SizeBytes: x.Photo.SizeBytes,
                    Caption: x.Photo.Caption,
                    PhotoCreatedAt: x.Photo.CreatedAt,
                    SavedAt: x.SavedAt
                );
            })
            .ToList();

        if (dto.Count == 0)
            return new SavedPhotosPageDto(Array.Empty<SavedPhotoItemDto>(), null);

        var lastVisible = visible[^1];
        var nextCursor = rawItems.Count < limit
            ? null
            : BuildCursor(lastVisible.SavedAt, lastVisible.SavedId);

        return new SavedPhotosPageDto(dto, nextCursor);
    }

    private static string BuildCursor(DateTime createdAt, Guid savedPhotoId)
        => $"{createdAt.ToUniversalTime():O}|{savedPhotoId:D}";

    private static bool TryParseCursor(string cursor, out DateTime? createdAt, out Guid? savedPhotoId)
    {
        createdAt = null;
        savedPhotoId = null;

        var parts = cursor.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!DateTime.TryParse(
                parts[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedCreatedAt))
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out var parsedSavedId))
            return false;

        createdAt = parsedCreatedAt;
        savedPhotoId = parsedSavedId;
        return true;
    }
}