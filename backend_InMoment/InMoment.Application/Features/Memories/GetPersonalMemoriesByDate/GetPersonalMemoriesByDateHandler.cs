using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Memories.GetPersonalMemoriesByDate;

public sealed class GetPersonalMemoriesByDateHandler
    : IRequestHandler<GetPersonalMemoriesByDateQuery, PersonalMemoriesByDateDto>
{
    private readonly IPhotoRepository _photos;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;

    public GetPersonalMemoriesByDateHandler(
        IPhotoRepository photos,
        IFileStorage storage,
        ICurrentUser current)
    {
        _photos = photos;
        _storage = storage;
        _current = current;
    }

    public async Task<PersonalMemoriesByDateDto> Handle(GetPersonalMemoriesByDateQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var fromUtc = q.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        var photos = await _photos.GetByUserAndDateRangeAsync(_current.UserId, fromUtc, toUtc, ct);

        var items = photos
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new PersonalMemoryPhotoDto(
                PhotoId: x.Id,
                GroupId: x.GroupId,
                PhotoUrl: _storage.GetPublicUrl(x.StorageKey),
                CreatedAt: x.CreatedAt))
            .ToList();

        return new PersonalMemoriesByDateDto(
            Date: q.Date,
            Items: items);
    }
}