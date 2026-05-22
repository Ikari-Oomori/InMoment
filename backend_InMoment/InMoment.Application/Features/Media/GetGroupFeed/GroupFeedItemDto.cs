using InMoment.Domain.Media;

namespace InMoment.Application.Features.Media.GetGroupFeed;

public sealed record FeedReactionCountDto(ReactionType Type, int Count);

public sealed record GroupFeedItemDto(
    Guid PhotoId,
    Guid GroupId,
    Guid AuthorId,
    string AuthorUserName,
    string? AuthorProfilePhotoUrl,
    string Url,
    string ContentType,
    long SizeBytes,
    string? Caption,
    DateTime CreatedAt,
    IReadOnlyList<FeedReactionCountDto> Reactions,
    int ReactionsCount,
    ReactionType MyReaction,
    int CommentsCount
);