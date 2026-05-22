using MediatR;

namespace InMoment.Application.Features.Memories.GetPersonalStats;

public sealed record GetPersonalMemoriesStatsQuery : IRequest<PersonalMemoriesStatsDto>;