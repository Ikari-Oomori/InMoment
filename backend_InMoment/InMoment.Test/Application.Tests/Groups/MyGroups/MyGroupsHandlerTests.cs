using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.MyGroups;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Groups.MyGroups;

public sealed class MyGroupsHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICurrentUser> _current = new();

    private MyGroupsHandler Create()
        => new(_groups.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenUserHasNoGroups()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());

        var handler = Create();

        var result = await handler.Handle(new MyGroupsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldMapGroupsToDtos()
    {
        var currentUserId = Guid.NewGuid();

        var first = Group.Create("Family", currentUserId);
        first.UpdateSettings(currentUserId, "Family", "Closest people");
        first.SetAvatar(currentUserId, "https://cdn.example.com/groups/family.jpg");

        var second = Group.Create("Work", currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var handler = Create();

        var result = await handler.Handle(new MyGroupsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].Id.Should().Be(first.Id);
        result[0].Name.Should().Be("Family");
        result[0].Description.Should().Be("Closest people");
        result[0].AvatarUrl.Should().Be("https://cdn.example.com/groups/family.jpg");
        result[0].OwnerId.Should().Be(first.OwnerId);
        result[0].CreatedAt.Should().Be(first.CreatedAt);

        result[1].Id.Should().Be(second.Id);
        result[1].Name.Should().Be("Work");
        result[1].Description.Should().BeNull();
        result[1].AvatarUrl.Should().BeNull();
        result[1].OwnerId.Should().Be(second.OwnerId);
        result[1].CreatedAt.Should().Be(second.CreatedAt);
    }
}