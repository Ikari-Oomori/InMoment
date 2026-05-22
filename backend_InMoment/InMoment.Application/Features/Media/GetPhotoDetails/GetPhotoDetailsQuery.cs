using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.GetPhotoDetails;

public sealed record GetPhotoDetailsQuery(Guid PhotoId) : IRequest<PhotoDetailsDto>;

public sealed record PhotoDetailsDto(
    Guid PhotoId,
    Guid GroupId,
    Guid AuthorId,
    string AuthorUserName,
    string AuthorFirstName,
    string AuthorLastName,
    string? AuthorProfilePhotoUrl,
    bool AuthorIsActive,
    string Url,
    string ContentType,
    long SizeBytes,
    string? Caption,
    DateTime CreatedAt,
    bool IsMine,
    bool CanEdit,
    bool CanDelete,
    ReactionType MyReaction,
    IReadOnlyList<PhotoReactionCountDto> Reactions,
    int CommentsCount
);

public sealed record PhotoReactionCountDto(
    ReactionType Type,
    int Count
);