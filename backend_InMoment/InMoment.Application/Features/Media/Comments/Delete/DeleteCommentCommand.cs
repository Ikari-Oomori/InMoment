using MediatR;

namespace InMoment.Application.Features.Media.Comments.Delete;

public sealed record DeleteCommentCommand(Guid CommentId) : IRequest;