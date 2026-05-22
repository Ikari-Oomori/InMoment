using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.LeaveGroup;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Groups.LeaveGroup;

public sealed class LeaveGroupHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private LeaveGroupHandler Create()
        => new(
            _groups.Object,
            _invitations.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(new LeaveGroupCommand(groupId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotMember()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerId);

        _current.SetupGet(x => x.UserId).Returns(outsiderId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(new LeaveGroupCommand(group.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");

        _invitations.Verify(x =>
            x.CancelPendingByInviterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _invitations.Verify(x =>
            x.CancelPendingByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeactivateMember_AndCancelOwnPendingInvites_WhenRegularMemberLeaves()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerId);
        group.AddMember(memberId);

        _current.SetupGet(x => x.UserId).Returns(memberId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        await handler.Handle(new LeaveGroupCommand(group.Id), CancellationToken.None);

        group.IsActive.Should().BeTrue();
        group.Members.Should().ContainSingle(x => x.UserId == memberId && !x.IsActive);

        _invitations.Verify(x =>
            x.CancelPendingByInviterAsync(group.Id, memberId, It.IsAny<CancellationToken>()),
            Times.Once);

        _invitations.Verify(x =>
            x.CancelPendingByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDeactivateGroup_AndCancelGroupPendingInvites_WhenLastOwnerLeaves()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Solo group", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        await handler.Handle(new LeaveGroupCommand(group.Id), CancellationToken.None);

        group.IsActive.Should().BeFalse();
        group.Members.Should().ContainSingle(x => x.UserId == ownerId && !x.IsActive);

        _invitations.Verify(x =>
            x.CancelPendingByInviterAsync(group.Id, ownerId, It.IsAny<CancellationToken>()),
            Times.Once);

        _invitations.Verify(x =>
            x.CancelPendingByGroupAsync(group.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenOwnerLeavesWithoutTransferingOwnership()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(memberId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(new LeaveGroupCommand(group.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Owner must transfer ownership before leaving.");

        group.IsActive.Should().BeTrue();
        group.Members.Should().ContainSingle(x => x.UserId == ownerId && x.IsActive && x.Role == GroupRole.Owner);

        _invitations.Verify(x =>
            x.CancelPendingByInviterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _invitations.Verify(x =>
            x.CancelPendingByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}