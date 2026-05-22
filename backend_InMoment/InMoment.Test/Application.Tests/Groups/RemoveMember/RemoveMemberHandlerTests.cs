using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.RemoveMember;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Groups.RemoveMember;

public sealed class RemoveMemberHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private RemoveMemberHandler Create()
        => new(
            _groups.Object,
            _invitations.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var actorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(actorId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new RemoveMemberCommand(groupId, targetId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _invitations.Verify(x => x.CancelPendingByInviterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenActorIsNotManager()
    {
        var ownerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(actorId);
        group.AddMember(targetId);

        _current.SetupGet(x => x.UserId).Returns(actorId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new RemoveMemberCommand(group.Id, targetId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only owner or admin can perform this action.");

        group.IsMember(targetId).Should().BeTrue();
        _invitations.Verify(x => x.CancelPendingByInviterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenAdminRemovesAnotherAdmin()
    {
        var ownerId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();
        var targetAdminId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(actingAdminId);
        group.AddMember(targetAdminId);
        group.PromoteToAdmin(ownerId, actingAdminId);
        group.PromoteToAdmin(ownerId, targetAdminId);

        _current.SetupGet(x => x.UserId).Returns(actingAdminId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new RemoveMemberCommand(group.Id, targetAdminId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Admin can remove only regular members.");

        group.IsMember(targetAdminId).Should().BeTrue();
        _invitations.Verify(x => x.CancelPendingByInviterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRemoveMember_AndCancelPendingInvitesByTarget_WhenActorIsOwner()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(targetId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new RemoveMemberCommand(group.Id, targetId),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.IsMember(targetId).Should().BeFalse();

        _invitations.Verify(x =>
            x.CancelPendingByInviterAsync(group.Id, targetId, It.IsAny<CancellationToken>()),
            Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRemoveRegularMember_WhenActorIsAdmin()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(adminId);
        group.AddMember(memberId);
        group.PromoteToAdmin(ownerId, adminId);

        _current.SetupGet(x => x.UserId).Returns(adminId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new RemoveMemberCommand(group.Id, memberId),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.IsMember(memberId).Should().BeFalse();

        _invitations.Verify(x =>
            x.CancelPendingByInviterAsync(group.Id, memberId, It.IsAny<CancellationToken>()),
            Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}