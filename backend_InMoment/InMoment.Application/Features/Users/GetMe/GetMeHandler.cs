using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Users.GetMe;

public sealed class GetMeHandler : IRequestHandler<GetMeQuery, MeDto>
{
    private readonly IUserRepository _users;
    private readonly IGroupRepository _groups;
    private readonly IInvitationRepository _invitations;
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;

    public GetMeHandler(
        IUserRepository users,
        IGroupRepository groups,
        IInvitationRepository invitations,
        ICurrentUser current,
        ISystemModeratorAccess moderatorAccess)
    {
        _users = users;
        _groups = groups;
        _invitations = invitations;
        _current = current;
        _moderatorAccess = moderatorAccess;
    }

    public async Task<MeDto> Handle(GetMeQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        var groups = await _groups.GetByUserIdAsync(user.Id, ct)
                     ?? Array.Empty<Group>();

        var invitations = await _invitations.GetPendingByInvitedUserIdAsync(user.Id, ct)
                          ?? Array.Empty<GroupInvitation>();

        var groupPreviews = groups
            .Select(x => new MyGroupPreviewDto(
                Id: x.Id,
                Name: x.Name,
                AvatarUrl: x.AvatarUrl,
                IsActiveGroup: user.ActiveGroupId.HasValue && user.ActiveGroupId.Value == x.Id))
            .OrderByDescending(x => x.IsActiveGroup)
            .ThenBy(x => x.Name)
            .ToList();

        var pendingInvitationPreviews = await BuildPendingInvitationPreviewsAsync(invitations, ct);

        var onboarding = new OnboardingStatusDto(
            IsCompleted: user.IsOnboardingCompleted,
            NeedsOnboarding: !user.IsOnboardingCompleted,
            HasCompletedContactsStep: user.HasCompletedContactsStep,
            SkippedContactsImport: user.SkippedContactsImport,
            CompletedAt: user.OnboardingCompletedAt,
            CanFinishOnboarding: user.HasCompletedContactsStep && !user.IsOnboardingCompleted);

        var profileCompleteness = new ProfileCompletenessDto(
            HasPhoneNumber: !string.IsNullOrWhiteSpace(user.PhoneNumber),
            HasProfilePhoto: !string.IsNullOrWhiteSpace(user.ProfilePhotoUrl),
            HasActiveGroup: user.ActiveGroupId.HasValue);

        return new MeDto(
            user.Id,
            user.Email,
            user.UserName,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.ProfilePhotoUrl,
            user.ActiveGroupId,
            user.CreatedAt,
            _moderatorAccess.IsModerator(user.Id),
            onboarding,
            profileCompleteness,
            groups.Count,
            invitations.Count,
            groupPreviews,
            pendingInvitationPreviews
        );
    }

    private async Task<IReadOnlyList<MyPendingInvitationPreviewDto>> BuildPendingInvitationPreviewsAsync(
        IReadOnlyList<GroupInvitation> invitations,
        CancellationToken ct)
    {
        if (invitations.Count == 0)
            return Array.Empty<MyPendingInvitationPreviewDto>();

        var inviterIds = invitations
            .Select(x => x.InvitedByUserId)
            .Distinct()
            .ToList();

        var inviters = await _users.GetByIdsAsync(inviterIds, ct)
                       ?? Array.Empty<User>();

        var inviterMap = inviters.ToDictionary(x => x.Id, x => x);

        var groupsMap = new Dictionary<Guid, Group>();

        foreach (var invitation in invitations)
        {
            if (groupsMap.ContainsKey(invitation.GroupId))
                continue;

            var group = await _groups.GetByIdAsync(invitation.GroupId, ct);
            if (group is not null)
                groupsMap[invitation.GroupId] = group;
        }

        var result = new List<MyPendingInvitationPreviewDto>();

        foreach (var invitation in invitations.OrderByDescending(x => x.CreatedAt))
        {
            if (!groupsMap.TryGetValue(invitation.GroupId, out var group))
                continue;

            inviterMap.TryGetValue(invitation.InvitedByUserId, out var inviter);

            result.Add(new MyPendingInvitationPreviewDto(
                InvitationId: invitation.Id,
                GroupId: group.Id,
                GroupName: group.Name,
                GroupAvatarUrl: group.AvatarUrl,
                InvitedByUserId: invitation.InvitedByUserId,
                InvitedByUserName: inviter?.UserName ?? "Unknown",
                InvitedByUserProfilePhotoUrl: inviter?.ProfilePhotoUrl,
                CreatedAt: invitation.CreatedAt));
        }

        return result;
    }
}