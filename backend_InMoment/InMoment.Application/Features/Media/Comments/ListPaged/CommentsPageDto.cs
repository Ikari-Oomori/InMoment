using InMoment.Domain.Media;

namespace InMoment.Application.Features.Media.Comments.ListPaged;

public sealed record PagedCommentDto(
    Guid Id,
    Guid PhotoId,
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    bool UserIsActive,
    Guid? ParentCommentId,
    Guid? ParentCommentUserId,
    string? ParentCommentUserName,
    bool? ParentCommentUserIsActive,
    string? ParentCommentTextPreview,
    string Text,
    string? GifUrl,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsMine,
    IReadOnlyList<PagedCommentReactionCountDto> Reactions,
    int ReactionsCount,
    ReactionType MyReaction
);

public sealed record CommentsPageDto(
    IReadOnlyList<PagedCommentDto> Items,
    string? NextCursor
);