namespace InMoment.Application.Features.Memories.GetPersonalCalendar;

public sealed record PersonalCalendarDayDto(
    int Day,
    bool HasPhotos,
    int PhotosCount,
    string? PreviewPhotoUrl
);

public sealed record PersonalCalendarDto(
    int Year,
    int Month,
    IReadOnlyList<PersonalCalendarDayDto> Days
);