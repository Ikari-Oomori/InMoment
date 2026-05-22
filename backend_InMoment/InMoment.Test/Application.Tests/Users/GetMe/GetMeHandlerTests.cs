using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Users.GetMe;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Users.GetMe;

public sealed class GetMeHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ISystemModeratorAccess> _moderatorAccess = new();

    public GetMeHandlerTests()
    {
        _moderatorAccess
            .Setup(x => x.IsModerator(It.IsAny<Guid>()))
            .Returns(false);
    }

    private GetMeHandler Create()
    => new(
        _users.Object,
        _groups.Object,
        _invitations.Object,
        _current.Object,
        _moderatorAccess.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new GetMeQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");

        _users.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _groups.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitations.Verify(x => x.GetPendingByInvitedUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(new GetMeQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");

        _groups.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitations.Verify(x => x.GetPendingByInvitedUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnUserProfileWithoutActiveGroup_WhenNotSet()
    {
        var user = User.Create(
            "profile@test.com",
            "hash",
            "profile_user",
            "Anna",
            "Petrova");

        var currentUserId = user.Id;

        var inviter1 = CreateUser(Guid.NewGuid(), "inviter_one");
        inviter1.SetProfilePhoto("https://cdn.example.com/u/inviter1.jpg");

        var inviter2 = CreateUser(Guid.NewGuid(), "inviter_two");

        var group1 = Group.Create("Group 1", inviter1.Id);
        group1.SetAvatar(inviter1.Id, "https://cdn.example.com/groups/g1.jpg");

        var group2 = Group.Create("Group 2", inviter2.Id);

        var invitation1 = GroupInvitation.Create(group1.Id, currentUserId, inviter1.Id);
        var invitation2 = GroupInvitation.Create(group2.Id, currentUserId, inviter2.Id);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { group2, group1 });

        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { invitation1, invitation2 });

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains(inviter1.Id) &&
                    ids.Contains(inviter2.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inviter1, inviter2 });

        _groups.Setup(x => x.GetByIdAsync(group1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group1);
        _groups.Setup(x => x.GetByIdAsync(group2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group2);

        var handler = Create();

        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("profile@test.com");
        result.UserName.Should().Be("profile_user");
        result.FirstName.Should().Be("Anna");
        result.LastName.Should().Be("Petrova");
        result.ProfilePhotoUrl.Should().BeNull();
        result.ActiveGroupId.Should().BeNull();
        result.CreatedAt.Should().Be(user.CreatedAt);
        result.IsSystemModerator.Should().BeFalse();
        result.GroupsCount.Should().Be(2);
        result.PendingInvitationsCount.Should().Be(2);

        result.Onboarding.IsCompleted.Should().BeFalse();
        result.Onboarding.NeedsOnboarding.Should().BeTrue();
        result.Onboarding.HasCompletedContactsStep.Should().BeFalse();
        result.Onboarding.SkippedContactsImport.Should().BeFalse();
        result.Onboarding.CompletedAt.Should().BeNull();
        result.Onboarding.CanFinishOnboarding.Should().BeFalse();

        result.ProfileCompleteness.HasPhoneNumber.Should().BeFalse();
        result.ProfileCompleteness.HasProfilePhoto.Should().BeFalse();
        result.ProfileCompleteness.HasActiveGroup.Should().BeFalse();

        result.Groups.Should().HaveCount(2);
        result.Groups[0].Id.Should().Be(group1.Id);
        result.Groups[0].Name.Should().Be("Group 1");
        result.Groups[0].AvatarUrl.Should().Be("https://cdn.example.com/groups/g1.jpg");
        result.Groups[0].IsActiveGroup.Should().BeFalse();

        result.Groups[1].Id.Should().Be(group2.Id);
        result.Groups[1].Name.Should().Be("Group 2");
        result.Groups[1].IsActiveGroup.Should().BeFalse();

        result.PendingInvitations.Should().HaveCount(2);
        result.PendingInvitations[0].InvitationId.Should().Be(invitation2.Id);
        result.PendingInvitations[0].GroupId.Should().Be(group2.Id);
        result.PendingInvitations[0].GroupName.Should().Be("Group 2");
        result.PendingInvitations[0].InvitedByUserId.Should().Be(inviter2.Id);
        result.PendingInvitations[0].InvitedByUserName.Should().Be("inviter_two");

        result.PendingInvitations[1].InvitationId.Should().Be(invitation1.Id);
        result.PendingInvitations[1].GroupId.Should().Be(group1.Id);
        result.PendingInvitations[1].GroupName.Should().Be("Group 1");
        result.PendingInvitations[1].GroupAvatarUrl.Should().Be("https://cdn.example.com/groups/g1.jpg");
        result.PendingInvitations[1].InvitedByUserId.Should().Be(inviter1.Id);
        result.PendingInvitations[1].InvitedByUserName.Should().Be("inviter_one");
        result.PendingInvitations[1].InvitedByUserProfilePhotoUrl.Should().Be("https://cdn.example.com/u/inviter1.jpg");
    }

    [Fact]
    public async Task Handle_ShouldReturnUserProfileWithPhotoActiveGroup_AndCompletedOnboarding()
    {
        var user = User.Create(
            "profile2@test.com",
            "hash",
            "profile_user_2",
            "Elena",
            "Sidorova",
            "+49 123 456 789");

        var currentUserId = user.Id;

        var activeGroup = Group.Create("A Group", currentUserId);
        activeGroup.SetAvatar(currentUserId, "https://cdn.example.com/groups/active.jpg");

        var anotherGroup = Group.Create("B Group", currentUserId);

        user.SetProfilePhoto("https://cdn.example.com/avatars/elena.jpg");
        user.SetActiveGroup(activeGroup.Id);
        user.MarkContactsStepCompleted(skipped: true);
        user.CompleteOnboarding();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { anotherGroup, activeGroup });
        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GroupInvitation>());
        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = Create();

        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        result.Id.Should().Be(user.Id);
        result.ProfilePhotoUrl.Should().Be("https://cdn.example.com/avatars/elena.jpg");
        result.ActiveGroupId.Should().Be(activeGroup.Id);
        result.GroupsCount.Should().Be(2);
        result.PendingInvitationsCount.Should().Be(0);
        result.IsSystemModerator.Should().BeFalse();

        result.Onboarding.IsCompleted.Should().BeTrue();
        result.Onboarding.NeedsOnboarding.Should().BeFalse();
        result.Onboarding.HasCompletedContactsStep.Should().BeTrue();
        result.Onboarding.SkippedContactsImport.Should().BeTrue();
        result.Onboarding.CompletedAt.Should().NotBeNull();
        result.Onboarding.CanFinishOnboarding.Should().BeFalse();

        result.ProfileCompleteness.HasPhoneNumber.Should().BeTrue();
        result.ProfileCompleteness.HasProfilePhoto.Should().BeTrue();
        result.ProfileCompleteness.HasActiveGroup.Should().BeTrue();

        result.Groups.Should().HaveCount(2);
        result.Groups[0].Id.Should().Be(activeGroup.Id);
        result.Groups[0].Name.Should().Be("A Group");
        result.Groups[0].AvatarUrl.Should().Be("https://cdn.example.com/groups/active.jpg");
        result.Groups[0].IsActiveGroup.Should().BeTrue();

        result.Groups[1].Id.Should().Be(anotherGroup.Id);
        result.Groups[1].Name.Should().Be("B Group");
        result.Groups[1].IsActiveGroup.Should().BeFalse();

        result.PendingInvitations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnCanFinishOnboarding_WhenContactsStepCompletedButNotFinished()
    {
        var user = User.Create(
            "profile3@test.com",
            "hash",
            "profile_user_3",
            "Mila",
            "Ivanova");

        user.MarkContactsStepCompleted(skipped: false);

        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());
        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GroupInvitation>());
        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = Create();

        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        result.Onboarding.IsCompleted.Should().BeFalse();
        result.Onboarding.NeedsOnboarding.Should().BeTrue();
        result.Onboarding.HasCompletedContactsStep.Should().BeTrue();
        result.Onboarding.SkippedContactsImport.Should().BeFalse();
        result.Onboarding.CompletedAt.Should().BeNull();
        result.Onboarding.CanFinishOnboarding.Should().BeTrue();
        result.IsSystemModerator.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnIsSystemModeratorTrue_WhenUserIsModerator()
    {
        var user = User.Create(
            "moderator@test.com",
            "hash",
            "moderator_user",
            "Ira",
            "Smirnova");

        var currentUserId = user.Id;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());
        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GroupInvitation>());
        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        _moderatorAccess.Setup(x => x.IsModerator(currentUserId)).Returns(true);

        var handler = Create();

        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        result.IsSystemModerator.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSkipPendingInvitations_WhenGroupNotFound()
    {
        var user = User.Create(
            "profile4@test.com",
            "hash",
            "profile_user_4",
            "Mila",
            "Ivanova");

        var currentUserId = user.Id;
        var inviterId = Guid.NewGuid();
        var missingGroupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(missingGroupId, currentUserId, inviterId);
        var inviter = CreateUser(inviterId, "missing_group_inviter");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());
        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { invitation });
        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inviter });
        _groups.Setup(x => x.GetByIdAsync(missingGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        result.PendingInvitationsCount.Should().Be(1);
        result.PendingInvitations.Should().BeEmpty();
    }

    private static User CreateUser(Guid id, string userName)
    {
        var user = User.Create(
            email: $"{userName}@test.com",
            passwordHash: "hash",
            userName: userName,
            firstName: "Test",
            lastName: "User");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);

        return user;
    }
}