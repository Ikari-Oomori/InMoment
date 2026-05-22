using InMoment.Domain.Media;

namespace InMoment.Application.Features.Discussions.ListGroupDiscussions;

public sealed record DiscussionReactionCountDto(
    ReactionType Type,
    int Count
);

public sealed record GroupDiscussionDto(
    Guid PhotoId,
    string PhotoUrl,
    DateTime PhotoCreatedAt,
    string? PhotoCaption,
    Guid PhotoAuthorUserId,
    string PhotoAuthorUserName,
    string? PhotoAuthorProfilePhotoUrl,
    IReadOnlyList<DiscussionReactionCountDto> Reactions,
    int ReactionsCount,
    ReactionType MyReaction,
    int CommentsCount,
    string? LatestCommentText,
    Guid? LatestCommentUserId,
    string? LatestCommentUserName,
    string? LatestCommentUserProfilePhotoUrl,
    DateTime? LatestCommentCreatedAt,
    DateTime LastActivityAt
);