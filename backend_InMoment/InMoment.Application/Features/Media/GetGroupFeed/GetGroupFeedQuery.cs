using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.GetGroupFeed;

public sealed record GetGroupFeedQuery(Guid GroupId, int Limit = 20)
    : IRequest<IReadOnlyList<GroupFeedItemDto>>;
