using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.ListPaged;

public sealed record PagedCommentReactionCountDto(
    ReactionType Type,
    int Count
);
public sealed record ListCommentsPageQuery(
    Guid PhotoId,
    int Limit = 20,
    string? Cursor = null
) : IRequest<CommentsPageDto>;