using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Memories.GetGroupMemoriesByDate;

public sealed class GetGroupMemoriesByDateHandler
    : IRequestHandler<GetGroupMemoriesByDateQuery, GroupMemoriesByDateDto>
{
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;

    public GetGroupMemoriesByDateHandler(
        IGroupRepository groups,
        IPhotoRepository photos,
        IFileStorage storage,
        ICurrentUser current)
    {
        _groups = groups;
        _photos = photos;
        _storage = storage;
        _current = current;
    }

    public async Task<GroupMemoriesByDateDto> Handle(GetGroupMemoriesByDateQuery q, CancellationToken ct)
    {
        if (q.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var group = await _groups.GetByIdAsync(q.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);

        var fromUtc = q.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        var photos = await _photos.GetByGroupAndDateRangeAsync(q.GroupId, fromUtc, toUtc, ct);

        var items = photos
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new GroupMemoryPhotoDto(
                PhotoId: x.Id,
                PhotoUrl: _storage.GetPublicUrl(x.StorageKey),
                CreatedAt: x.CreatedAt,
                UploadedByUserId: x.UploadedByUserId))
            .ToList();

        return new GroupMemoriesByDateDto(
            q.GroupId,
            q.Date,
            items);
    }
}