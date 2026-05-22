using InMoment.Domain.Groups;

namespace InMoment.Application.Features.Groups.GetMembers;

public sealed record GroupMemberDto(
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    GroupRole Role,
    bool IsOwner,
    bool IsAdmin
);