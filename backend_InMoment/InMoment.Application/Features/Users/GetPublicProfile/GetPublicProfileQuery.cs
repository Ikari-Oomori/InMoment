using MediatR;

namespace InMoment.Application.Features.Users.GetPublicProfile;

public sealed record GetPublicProfileQuery(Guid UserId) : IRequest<PublicUserProfileDto>;

public sealed record PublicUserProfileDto(
    Guid Id,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    DateTime CreatedAt,
    bool IsBlockedByMe,
    bool HasBlockedMe,
    bool IsActive,
    bool CanBlock,
    bool CanReport
);