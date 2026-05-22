using MediatR;

namespace InMoment.Application.Features.Memories.GetPersonalCalendar;

public sealed record GetPersonalCalendarQuery(
    int Year,
    int Month
) : IRequest<PersonalCalendarDto>;