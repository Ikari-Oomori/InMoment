namespace InMoment.Application.Features.Media.GetGroupFeed;

public sealed record FeedPageDto(
    IReadOnlyList<GroupFeedItemDto> Items,
    string? NextCursor
);