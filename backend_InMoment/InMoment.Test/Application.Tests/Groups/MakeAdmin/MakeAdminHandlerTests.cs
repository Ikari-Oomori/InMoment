using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.MakeAdmin;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Groups.MakeAdmin;

public sealed class MakeAdminHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private MakeAdminHandler Create()
        => new(_groups.Object, _uow.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var ownerId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new MakeAdminCommand(groupId, targetUserId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenActorIsNotOwner()
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

        var act = () => handler.Handle(
            new MakeAdminCommand(group.Id, memberId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only owner can perform this action.");

        group.IsAdmin(memberId).Should().BeFalse();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPromoteMemberToAdmin_WhenActorIsOwner()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(memberId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new MakeAdminCommand(group.Id, memberId),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.IsAdmin(memberId).Should().BeTrue();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSaveChanges_WhenTargetAlreadyAdmin()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(adminId);
        group.PromoteToAdmin(ownerId, adminId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new MakeAdminCommand(group.Id, adminId),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.IsAdmin(adminId).Should().BeTrue();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}