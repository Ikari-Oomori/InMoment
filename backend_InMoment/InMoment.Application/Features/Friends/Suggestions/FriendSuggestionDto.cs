namespace InMoment.Application.Features.Friends.Suggestions;

public sealed record FriendSuggestionDto(
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    bool AlreadyFriend,
    bool HasIncomingRequest,
    bool HasOutgoingRequest
);