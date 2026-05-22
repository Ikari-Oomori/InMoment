using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.Settings;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using Moq;

namespace InMoment.Tests.Application.Groups.Settings;

public sealed class GroupSettingsHandlersTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private static Group CreateGroup(Guid ownerId, Guid memberId)
    {
        var group = Group.Create("test", ownerId);
        group.AddMember(memberId);
        return group;
    }

    [Fact]
    public async Task GetSettings_Should_Work_For_Member()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        var group = CreateGroup(owner, member);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(member);

        var handler = new GetGroupSettingsHandler(_groups.Object, _current.Object);

        var result = await handler.Handle(new GetGroupSettingsQuery(group.Id), default);

        result.Id.Should().Be(group.Id);
        result.Name.Should().Be("test");
    }

    [Fact]
    public async Task GetSettings_Should_Throw_For_NonMember()
    {
        var owner = Guid.NewGuid();
        var outsider = Guid.NewGuid();

        var group = Group.Create("test", owner);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(outsider);

        var handler = new GetGroupSettingsHandler(_groups.Object, _current.Object);

        var act = () => handler.Handle(new GetGroupSettingsQuery(group.Id), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateSettings_Should_Work_For_Owner()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        var group = CreateGroup(owner, member);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(owner);

        var handler = new UpdateGroupSettingsHandler(
            _groups.Object, _uow.Object, _current.Object);

        var result = await handler.Handle(
            new UpdateGroupSettingsCommand(group.Id, "new", "desc"), default);

        result.Name.Should().Be("new");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_Should_Throw_For_NonOwner()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        var group = CreateGroup(owner, member);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(member);

        var handler = new UpdateGroupSettingsHandler(
            _groups.Object, _uow.Object, _current.Object);

        var act = () => handler.Handle(
            new UpdateGroupSettingsCommand(group.Id, "new", null), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task SetAvatar_Should_Work_For_Owner()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        var group = CreateGroup(owner, member);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(owner);

        var handler = new SetGroupAvatarHandler(
            _groups.Object, _uow.Object, _current.Object);

        await handler.Handle(
            new SetGroupAvatarCommand(group.Id, "url"), default);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAvatar_Should_Throw_For_NonOwner()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        var group = CreateGroup(owner, member);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(member);

        var handler = new SetGroupAvatarHandler(
            _groups.Object, _uow.Object, _current.Object);

        var act = () => handler.Handle(
            new SetGroupAvatarCommand(group.Id, "url"), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}