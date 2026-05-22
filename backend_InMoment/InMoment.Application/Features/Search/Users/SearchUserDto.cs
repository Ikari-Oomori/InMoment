namespace InMoment.Application.Features.Search.Users;

public sealed record SearchUserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl
);