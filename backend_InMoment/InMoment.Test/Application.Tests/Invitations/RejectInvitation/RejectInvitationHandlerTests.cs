using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Invitations.RejectInvitation;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Invitations.RejectInvitation;

public sealed class RejectInvitationHandlerTests
{
    private readonly Mock<IInvitationRepository> _invitations = new();
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
        var command = new RejectInvitationCommand(invitationId);

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
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new RejectInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot reject this invitation.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenInvitationIsNotPending()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, currentUserId, inviterUserId);
        invitation.Accept();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new RejectInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only pending invitations can be rejected.");
    }

    [Fact]
    public async Task Handle_ShouldRejectInvitation_WhenValid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, currentUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new RejectInvitationCommand(invitation.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);

        invitation.Status.Should().Be(InvitationStatus.Rejected);
        invitation.RespondedAt.Should().NotBeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotSaveChanges_WhenValidationFails()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = GroupInvitation.Create(groupId, invitedUserId, inviterUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations
            .Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var handler = CreateHandler();
        var command = new RejectInvitationCommand(invitation.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot reject this invitation.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private RejectInvitationHandler CreateHandler()
        => new(
            _invitations.Object,
            _uow.Object,
            _current.Object);
}