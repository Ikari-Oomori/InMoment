using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.CommentReactions.GetSummary;

public sealed record GetCommentReactionsSummaryQuery(Guid CommentId) : IRequest<CommentReactionsSummaryDto>;

public sealed record CommentReactionsSummaryDto(
    Guid CommentId,
    ReactionType MyReaction,
    IReadOnlyList<CommentReactionCountDto> Counts
);

public sealed record CommentReactionCountDto(ReactionType Type, int Count);