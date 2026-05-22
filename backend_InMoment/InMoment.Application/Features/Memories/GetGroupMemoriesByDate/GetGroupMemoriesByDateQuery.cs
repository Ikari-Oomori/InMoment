using MediatR;

namespace InMoment.Application.Features.Memories.GetGroupMemoriesByDate;

public sealed record GetGroupMemoriesByDateQuery(
    Guid GroupId,
    DateOnly Date
) : IRequest<GroupMemoriesByDateDto>;