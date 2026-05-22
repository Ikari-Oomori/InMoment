namespace InMoment.Application.Features.Contacts.Common;

public sealed record ContactMatchDto(
    Guid UserId,
    string UserName,
    string FirstName,
    string LastName,
    string? ProfilePhotoUrl,
    string MatchedBy,
    string? MatchedValue,
    string? SourceContactDisplayName,
    bool AlreadyFriend,
    bool HasIncomingRequest,
    bool HasOutgoingRequest,
    bool CanSendFriendRequest
);