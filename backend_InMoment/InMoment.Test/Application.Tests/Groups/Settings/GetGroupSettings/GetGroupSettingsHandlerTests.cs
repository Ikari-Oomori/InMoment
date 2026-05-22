using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.Settings;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Groups.Settings.GetGroupSettings;

public sealed class GetGroupSettingsHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetGroupSettingsHandler Create()
        => new(_groups.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupIdEmpty()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupSettingsQuery(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
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
            new GetGroupSettingsQuery(groupId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotMember()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var group = Group.Create("Family", ownerId);

        _current.SetupGet(x => x.UserId).Returns(outsiderId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupSettingsQuery(group.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldReturnGroupSettingsDto()
    {
        var ownerId = Guid.NewGuid();
        var group = Group.Create("Family", ownerId);
        group.UpdateSettings(ownerId, "Family Updated", "Closest people");
        group.SetAvatar(ownerId, "https://cdn.example.com/groups/family.jpg");

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupSettingsQuery(group.Id),
            CancellationToken.None);

        result.Id.Should().Be(group.Id);
        result.Name.Should().Be("Family Updated");
        result.Description.Should().Be("Closest people");
        result.AvatarUrl.Should().Be("https://cdn.example.com/groups/family.jpg");
        result.OwnerId.Should().Be(ownerId);
        result.CreatedAt.Should().Be(group.CreatedAt);
    }
}