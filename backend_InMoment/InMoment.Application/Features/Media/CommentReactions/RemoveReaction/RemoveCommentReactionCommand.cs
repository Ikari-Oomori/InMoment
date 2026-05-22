using MediatR;

namespace InMoment.Application.Features.Media.CommentReactions.RemoveReaction;

public sealed record RemoveCommentReactionCommand(Guid CommentId) : IRequest;