using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Memories.GetGroupCalendar;

public sealed class GetGroupCalendarHandler
    : IRequestHandler<GetGroupCalendarQuery, GroupCalendarDto>
{
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;

    public GetGroupCalendarHandler(
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

    public async Task<GroupCalendarDto> Handle(GetGroupCalendarQuery q, CancellationToken ct)
    {
        if (q.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        if (q.Year is < 2000 or > 2100)
            throw new ValidationException("Некорректный год.");

        if (q.Month is < 1 or > 12)
            throw new ValidationException("Некорректный месяц.");

        var group = await _groups.GetByIdAsync(q.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);

        var fromUtc = new DateTime(q.Year, q.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddMonths(1);

        var photos = await _photos.GetByGroupAndDateRangeAsync(q.GroupId, fromUtc, toUtc, ct);

        var activePhotos = photos
            .Where(x => !x.IsDeleted)
            .ToList();

        var byDay = activePhotos
            .GroupBy(x => x.CreatedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt)
                      .ThenByDescending(x => x.Id)
                      .ToList());

        var daysInMonth = DateTime.DaysInMonth(q.Year, q.Month);
        var days = new List<GroupCalendarDayDto>(daysInMonth);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var key = new DateTime(q.Year, q.Month, day, 0, 0, 0, DateTimeKind.Utc).Date;

            if (!byDay.TryGetValue(key, out var dayPhotos) || dayPhotos.Count == 0)
            {
                days.Add(new GroupCalendarDayDto(
                    Day: day,
                    HasPhotos: false,
                    PhotosCount: 0,
                    PreviewPhotoUrl: null));
                continue;
            }

            var preview = dayPhotos[0];

            days.Add(new GroupCalendarDayDto(
                Day: day,
                HasPhotos: true,
                PhotosCount: dayPhotos.Count,
                PreviewPhotoUrl: _storage.GetPublicUrl(preview.StorageKey)));
        }

        return new GroupCalendarDto(
            GroupId: q.GroupId,
            Year: q.Year,
            Month: q.Month,
            Days: days);
    }
}