using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Users.SetActiveGroup;
using InMoment.Domain.Common;
using InMoment.Domain.Users;
using DomainGroup = InMoment.Domain.Groups.Group;

namespace InMoment.Application.Tests.Users.SetActiveGroup;

public sealed class SetActiveGroupHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SetActiveGroupHandler Create()
        => new(
            _users.Object,
            _groups.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new SetActiveGroupCommand(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new SetActiveGroupCommand(groupId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var user = User.Create(
            "user@test.com",
            "hash",
            "user_test",
            "User",
            "Test");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new SetActiveGroupCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotMemberOfGroup()
    {
        var currentUserId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var user = User.Create(
            "user@test.com",
            "hash",
            "user_test",
            "User",
            "Test");

        var group = DomainGroup.Create("Secret group", ownerId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new SetActiveGroupCommand(group.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");

        user.ActiveGroupId.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSetActiveGroup_WhenCurrentUserIsMember()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "user@test.com",
            "hash",
            "user_test",
            "User",
            "Test");

        var group = DomainGroup.Create("My group", currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        await handler.Handle(new SetActiveGroupCommand(group.Id), CancellationToken.None);

        user.ActiveGroupId.Should().Be(group.Id);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}