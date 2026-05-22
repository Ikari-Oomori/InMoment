using MediatR;

namespace InMoment.Application.Features.Groups.MyGroups;

public sealed record MyGroupsQuery() : IRequest<IReadOnlyList<MyGroupDto>>;