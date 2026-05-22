using MediatR;

namespace InMoment.Application.Features.Memories.GetGroupCalendar;

public sealed record GetGroupCalendarQuery(
    Guid GroupId,
    int Year,
    int Month
) : IRequest<GroupCalendarDto>;