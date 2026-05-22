using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.TransferOwnership;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Groups.TransferOwnership;

public sealed class TransferOwnershipHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IAppTransaction> _tx = new();

    private TransferOwnershipHandler Create()
        => new(
            _groups.Object,
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

        var act = () => handler.Handle(
            new TransferOwnershipCommand(groupId, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldTransferOwnership_WhenCurrentUserIsOwner_AndTargetIsActiveMember()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(memberId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tx.Object);

        var handler = Create();

        var result = await handler.Handle(
            new TransferOwnershipCommand(group.Id, memberId),
            CancellationToken.None);

        result.Should().Be(Unit.Value);

        group.OwnerId.Should().Be(memberId);

        group.Members.Should().ContainSingle(x =>
            x.UserId == ownerId &&
            x.IsActive &&
            x.Role == GroupRole.Admin);

        group.Members.Should().ContainSingle(x =>
            x.UserId == memberId &&
            x.IsActive &&
            x.Role == GroupRole.Owner);

        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotOwner()
    {
        var ownerId = Guid.NewGuid();
        var nonOwnerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(nonOwnerId);
        group.AddMember(targetId);

        _current.SetupGet(x => x.UserId).Returns(nonOwnerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tx.Object);

        var handler = Create();

        var act = () => handler.Handle(
            new TransferOwnershipCommand(group.Id, targetId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only owner can perform this action.");

        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenTargetIsNotMember()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tx.Object);

        var handler = Create();

        var act = () => handler.Handle(
            new TransferOwnershipCommand(group.Id, outsiderId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("New owner must be a member of the group.");

        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}