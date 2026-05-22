using MediatR;

namespace InMoment.Application.Features.Media.GetGroupFeed;

public sealed record GetGroupFeedPageQuery(
    Guid GroupId,
    int Limit = 20,
    string? Cursor = null
) : IRequest<FeedPageDto>;