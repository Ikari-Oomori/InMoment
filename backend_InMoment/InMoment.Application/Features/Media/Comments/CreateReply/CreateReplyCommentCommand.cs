using MediatR;

namespace InMoment.Application.Features.Media.Comments.CreateReply;

public sealed record CreateReplyCommentCommand(
    Guid PhotoId,
    Guid ParentCommentId,
    string? Text,
    string? GifUrl = null
) : IRequest<Guid>;