using MediatR;

namespace InMoment.Application.Features.Memories.GetPersonalMemoriesByDate;

public sealed record GetPersonalMemoriesByDateQuery(
    DateOnly Date
) : IRequest<PersonalMemoriesByDateDto>;