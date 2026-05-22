using MediatR;

namespace InMoment.Application.Features.Users.GetMe;

public sealed record GetMeQuery() : IRequest<MeDto>;

public sealed record MyGroupPreviewDto(
    Guid Id,
    string Name,
    string? AvatarUrl,
    bool IsActiveGroup
);

public sealed record MyPendingInvitationPreviewDto(
    Guid InvitationId,
    Guid GroupId,
    string GroupName,
    string? GroupAvatarUrl,
    Guid InvitedByUserId,
    string InvitedByUserName,
    string? InvitedByUserProfilePhotoUrl,
    DateTime CreatedAt
);

public sealed record OnboardingStatusDto(
    bool IsCompleted,
    bool NeedsOnboarding,
    bool HasCompletedContactsStep,
    bool SkippedContactsImport,
    DateTime? CompletedAt,
    bool CanFinishOnboarding
);

public sealed record ProfileCompletenessDto(
    bool HasPhoneNumber,
    bool HasProfilePhoto,
    bool HasActiveGroup
);

public sealed record MeDto(
    Guid Id,
    string Email,
    string UserName,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? ProfilePhotoUrl,
    Guid? ActiveGroupId,
    DateTime CreatedAt,
    bool IsSystemModerator,
    OnboardingStatusDto Onboarding,
    ProfileCompletenessDto ProfileCompleteness,
    int GroupsCount,
    int PendingInvitationsCount,
    IReadOnlyList<MyGroupPreviewDto> Groups,
    IReadOnlyList<MyPendingInvitationPreviewDto> PendingInvitations
);