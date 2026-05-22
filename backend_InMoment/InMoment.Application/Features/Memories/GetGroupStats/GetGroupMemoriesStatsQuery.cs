using MediatR;

namespace InMoment.Application.Features.Memories.GetGroupStats;

public sealed record GetGroupMemoriesStatsQuery(Guid GroupId) : IRequest<GroupMemoriesStatsDto>;