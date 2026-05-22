using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Invitations.AcceptInvitation;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Invitations.AcceptInvitation;

public sealed class AcceptInvitationHandlerTests
{
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenInvitationNotFound()
    {
        // Arrange
        var invitationId = Guid.NewGuid();

        _invitations.Setup(x => x.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupInvitation?)null);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitationId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Invitation not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotInvitee()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot accept this invitation.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenInvitationIsNotPending()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, currentUserId, inviterUserId);
        invitation.Reject();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only pending invitations can be accepted.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenInviterIsNoLongerActiveMember()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, currentUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _invitations.Setup(x => x.InviterIsActiveMemberAsync(groupId, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Invitation is no longer valid.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, currentUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _invitations.Setup(x => x.InviterIsActiveMemberAsync(groupId, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserIsAlreadyMember()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var group = Group.Create("Test group", inviterUserId);
        group.AddMember(currentUserId);

        var invitation = GroupInvitation.Create(group.Id, currentUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _invitations.Setup(x => x.InviterIsActiveMemberAsync(group.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("User is already a member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldAcceptInvitation_AndAddMember_WhenValid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var group = Group.Create("Test group", inviterUserId);
        var invitation = GroupInvitation.Create(group.Id, currentUserId, inviterUserId);

        GroupMember? addedMember = null;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _invitations.Setup(x => x.InviterIsActiveMemberAsync(group.Id, inviterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _groups.Setup(x => x.AddMemberAsync(It.IsAny<GroupMember>(), It.IsAny<CancellationToken>()))
            .Callback<GroupMember, CancellationToken>((member, _) => addedMember = member)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);

        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.RespondedAt.Should().NotBeNull();

        addedMember.Should().NotBeNull();
        addedMember!.GroupId.Should().Be(group.Id);
        addedMember.UserId.Should().Be(currentUserId);
        addedMember.Role.Should().Be(GroupRole.Member);
        addedMember.IsActive.Should().BeTrue();

        _groups.Verify(x => x.AddMemberAsync(It.IsAny<GroupMember>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotSaveChanges_WhenValidationFailsBeforeMutation()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new AcceptInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _groups.Verify(x => x.AddMemberAsync(It.IsAny<GroupMember>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private AcceptInvitationHandler CreateHandler()
        => new(
            _invitations.Object,
            _groups.Object,
            _uow.Object,
            _current.Object);
}