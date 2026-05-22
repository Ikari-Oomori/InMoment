using MediatR;

namespace InMoment.Application.Features.Search.Groups;

public sealed record SearchMyGroupsQuery(string Query, int Limit = 10)
    : IRequest<IReadOnlyList<SearchGroupDto>>;