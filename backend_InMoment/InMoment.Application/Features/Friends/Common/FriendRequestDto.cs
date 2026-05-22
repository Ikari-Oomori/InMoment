using InMoment.Domain.Friends;

namespace InMoment.Application.Features.Friends.Common;

public sealed record FriendRequestDto(
    Guid RequestId,
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    FriendRequestStatus Status,
    DateTime CreatedAtUtc
);