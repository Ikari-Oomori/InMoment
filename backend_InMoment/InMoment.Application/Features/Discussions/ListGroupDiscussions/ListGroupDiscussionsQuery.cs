using MediatR;

namespace InMoment.Application.Features.Discussions.ListGroupDiscussions;

public sealed record ListGroupDiscussionsQuery(
    Guid GroupId,
    int Limit = 30
) : IRequest<IReadOnlyList<GroupDiscussionDto>>;