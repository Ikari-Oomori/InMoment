using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.Settings;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Groups.Settings.UpdateGroupSettings;

public sealed class UpdateGroupSettingsHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private UpdateGroupSettingsHandler Create()
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
            new UpdateGroupSettingsCommand(Guid.Empty, "Name", "Desc"),
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
            new UpdateGroupSettingsCommand(groupId, "Name", "Desc"),
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
            new UpdateGroupSettingsCommand(group.Id, "New name", "New desc"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only owner can perform this action.");

        group.Name.Should().Be("Team");
        group.Description.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUpdateSettings_SaveChanges_AndReturnDto()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateGroupSettingsCommand(group.Id, "New Team", "Private team"),
            CancellationToken.None);

        result.Id.Should().Be(group.Id);
        result.Name.Should().Be("New Team");
        result.Description.Should().Be("Private team");
        result.AvatarUrl.Should().BeNull();
        result.OwnerId.Should().Be(ownerId);
        result.CreatedAt.Should().Be(group.CreatedAt);

        group.Name.Should().Be("New Team");
        group.Description.Should().Be("Private team");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}