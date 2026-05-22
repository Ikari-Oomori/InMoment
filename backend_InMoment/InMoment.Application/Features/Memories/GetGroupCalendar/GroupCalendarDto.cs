namespace InMoment.Application.Features.Memories.GetGroupCalendar;

public sealed record GroupCalendarDayDto(
    int Day,
    bool HasPhotos,
    int PhotosCount,
    string? PreviewPhotoUrl
);

public sealed record GroupCalendarDto(
    Guid GroupId,
    int Year,
    int Month,
    IReadOnlyList<GroupCalendarDayDto> Days
);