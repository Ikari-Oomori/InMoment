using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.List;

public sealed record ListCommentsQuery(Guid PhotoId, int Limit = 50) : IRequest<IReadOnlyList<CommentDto>>;

public sealed record CommentListReactionCountDto(
    ReactionType Type,
    int Count
);

public sealed record CommentDto(
    Guid Id,
    Guid PhotoId,
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    Guid? ParentCommentId,
    Guid? ParentCommentUserId,
    string? ParentCommentUserName,
    string? ParentCommentTextPreview,
    string Text,
    string? GifUrl,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsMine,
    IReadOnlyList<CommentListReactionCountDto> Reactions,
    int ReactionsCount,
    ReactionType MyReaction
);