namespace InMoment.Application.Features.Groups.MyGroups;

public sealed record MyGroupDto(
    Guid Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    Guid OwnerId,
    bool IsAdmin,
    int MembersCount,
    DateTime CreatedAt
);