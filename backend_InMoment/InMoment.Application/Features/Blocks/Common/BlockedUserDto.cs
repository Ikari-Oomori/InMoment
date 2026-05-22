namespace InMoment.Application.Features.Blocks.Common;

public sealed record BlockedUserDto(
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    DateTime BlockedAtUtc
);