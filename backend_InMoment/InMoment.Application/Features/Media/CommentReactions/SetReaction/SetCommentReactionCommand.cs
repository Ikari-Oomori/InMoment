using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.CommentReactions.SetReaction;

public sealed record SetCommentReactionCommand(Guid CommentId, ReactionType Type) : IRequest;