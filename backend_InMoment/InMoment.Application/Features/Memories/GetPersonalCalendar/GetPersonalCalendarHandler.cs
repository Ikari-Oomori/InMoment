using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Memories.GetPersonalCalendar;

public sealed class GetPersonalCalendarHandler
    : IRequestHandler<GetPersonalCalendarQuery, PersonalCalendarDto>
{
    private readonly IPhotoRepository _photos;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;

    public GetPersonalCalendarHandler(
        IPhotoRepository photos,
        IFileStorage storage,
        ICurrentUser current)
    {
        _photos = photos;
        _storage = storage;
        _current = current;
    }

    public async Task<PersonalCalendarDto> Handle(GetPersonalCalendarQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (q.Year is < 2000 or > 2100)
            throw new ValidationException("Некорректный год.");

        if (q.Month is < 1 or > 12)
            throw new ValidationException("Некорректный месяц.");

        var fromUtc = new DateTime(q.Year, q.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddMonths(1);

        var photos = await _photos.GetByUserAndDateRangeAsync(_current.UserId, fromUtc, toUtc, ct);

        var byDay = photos
            .GroupBy(x => x.CreatedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt)
                      .ThenByDescending(x => x.Id)
                      .ToList());

        var daysInMonth = DateTime.DaysInMonth(q.Year, q.Month);
        var days = new List<PersonalCalendarDayDto>(daysInMonth);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var key = new DateTime(q.Year, q.Month, day, 0, 0, 0, DateTimeKind.Utc).Date;

            if (!byDay.TryGetValue(key, out var dayPhotos) || dayPhotos.Count == 0)
            {
                days.Add(new PersonalCalendarDayDto(
                    Day: day,
                    HasPhotos: false,
                    PhotosCount: 0,
                    PreviewPhotoUrl: null));
                continue;
            }

            var preview = dayPhotos[0];

            days.Add(new PersonalCalendarDayDto(
                Day: day,
                HasPhotos: true,
                PhotosCount: dayPhotos.Count,
                PreviewPhotoUrl: _storage.GetPublicUrl(preview.StorageKey)));
        }

        return new PersonalCalendarDto(
            Year: q.Year,
            Month: q.Month,
            Days: days);
    }
}