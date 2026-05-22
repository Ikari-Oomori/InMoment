using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.Reactions.GetSummary;

public sealed record GetReactionsSummaryQuery(Guid PhotoId) : IRequest<ReactionsSummaryDto>;

public sealed record ReactionsSummaryDto(
    Guid PhotoId,
    ReactionType MyReaction,
    IReadOnlyList<ReactionCountDto> Counts
);

public sealed record ReactionCountDto(ReactionType Type, int Count);