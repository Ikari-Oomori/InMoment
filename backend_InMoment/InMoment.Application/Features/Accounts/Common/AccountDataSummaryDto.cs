namespace InMoment.Application.Features.Accounts.Common;

public sealed record AccountDataSummaryDto(
    Guid UserId,
    bool IsActive,
    int GroupsCount,
    int OwnedGroupsCount,
    int PhotosCount,
    int CommentsCount,
    int ReactionsCount,
    int FriendshipsCount,
    int ActiveSessionsCount
);