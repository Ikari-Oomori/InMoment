using MediatR;

namespace InMoment.Application.Features.Media.Comments.CreateRoot;

public sealed record CreateRootCommentCommand(
    Guid PhotoId,
    string? Text,
    string? GifUrl = null
) : IRequest<Guid>;