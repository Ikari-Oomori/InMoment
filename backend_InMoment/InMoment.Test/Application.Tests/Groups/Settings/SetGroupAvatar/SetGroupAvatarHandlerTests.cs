using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.Settings;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Tests.Groups.Settings.SetGroupAvatar;

public sealed class SetGroupAvatarHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SetGroupAvatarHandler Create()
        => new(_groups.Object, _uow.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupIdEmpty()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new SetGroupAvatarCommand(Guid.Empty, "https://cdn.example.com/groups/a.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new SetGroupAvatarCommand(groupId, "https://cdn.example.com/groups/a.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenActorIsNotOwner()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(memberId);

        _current.SetupGet(x => x.UserId).Returns(memberId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new SetGroupAvatarCommand(group.Id, "https://cdn.example.com/groups/new.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only owner can perform this action.");

        group.AvatarUrl.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSetAvatar_WhenUrlIsArbitraryString_AndActorIsOwner()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new SetGroupAvatarCommand(group.Id, "not-a-valid-url"),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.AvatarUrl.Should().Be("not-a-valid-url");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSetAvatar_WhenUrlIsRelativeLikeString_AndActorIsOwner()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new SetGroupAvatarCommand(group.Id, "/groups/new.jpg"),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.AvatarUrl.Should().Be("/groups/new.jpg");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSetAvatar_AndSaveChanges()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new SetGroupAvatarCommand(group.Id, "https://cdn.example.com/groups/new.jpg"),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.AvatarUrl.Should().Be("https://cdn.example.com/groups/new.jpg");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowHttpAvatarUrl()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new SetGroupAvatarCommand(group.Id, "http://localhost:9000/inmoment/groups/new.jpg"),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.AvatarUrl.Should().Be("http://localhost:9000/inmoment/groups/new.jpg");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowNullAvatarUrl_AndClearValue()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);
        group.SetAvatar(ownerId, "https://cdn.example.com/groups/old.jpg");

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new SetGroupAvatarCommand(group.Id, null),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.AvatarUrl.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowWhitespaceAvatarUrl_AndClearValue()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);
        group.SetAvatar(ownerId, "https://cdn.example.com/groups/old.jpg");

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new SetGroupAvatarCommand(group.Id, "   "),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        group.AvatarUrl.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}