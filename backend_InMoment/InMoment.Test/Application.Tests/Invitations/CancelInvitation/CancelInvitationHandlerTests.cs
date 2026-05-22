using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Invitations.CancelInvitation;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Invitations.CancelInvitation;

public sealed class CancelInvitationHandlerTests
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

        _invitations
            .Setup(x => x.GetByIdAsync(invitationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupInvitation?)null);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitationId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Invitation not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenInvitationIsNotPending()
    {
        // Arrange
        var inviterUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);
        invitation.Reject();

        _current.SetupGet(x => x.UserId).Returns(inviterUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only pending invitations can be cancelled.");
    }

    [Fact]
    public async Task Handle_ShouldCancelInvitation_WhenCurrentUserIsInviter()
    {
        // Arrange
        var inviterUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(inviterUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitation.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);

        invitation.Status.Should().Be(InvitationStatus.Cancelled);
        invitation.RespondedAt.Should().NotBeNull();

        _groups.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound_AndCurrentUserIsNotInviter()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(ownerUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groups
            .Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotInviterAndNotOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(otherUserId);

        var invitation = GroupInvitation.Create(group.Id, Guid.NewGuid(), inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(otherUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groups
            .Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only owner can perform this action.");

        invitation.Status.Should().Be(InvitationStatus.Pending);
        invitation.RespondedAt.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCancelInvitation_WhenCurrentUserIsOwnerAndNotInviter()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerUserId);
        var invitation = GroupInvitation.Create(group.Id, invitedUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(ownerUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groups
            .Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitation.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);

        invitation.Status.Should().Be(InvitationStatus.Cancelled);
        invitation.RespondedAt.Should().NotBeNull();

        _groups.Verify(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotSaveChanges_WhenValidationFails()
    {
        // Arrange
        var inviterUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);
        invitation.Accept();

        _current.SetupGet(x => x.UserId).Returns(otherUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new CancelInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only pending invitations can be cancelled.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private CancelInvitationHandler CreateHandler()
        => new(
            _invitations.Object,
            _groups.Object,
            _uow.Object,
            _current.Object);
}