using MediatR;

namespace InMoment.Application.Features.Media.Comments.Edit;

public sealed record EditCommentCommand(
    Guid CommentId,
    string Text
) : IRequest<Guid>;