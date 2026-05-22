namespace InMoment.Application.Features.Friends.Common;

public sealed record FriendDto(
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    DateTime FriendsSinceUtc
);