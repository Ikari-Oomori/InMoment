using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Search.Groups;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Search.Groups;

public sealed class SearchMyGroupsHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SearchMyGroupsHandler Create()
        => new(_groups.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenQueryBlank()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        var result = await handler.Handle(
            new SearchMyGroupsQuery("   "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _groups.Verify(
            x => x.SearchMyGroupsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitOutOfRange()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _groups.Setup(x => x.SearchMyGroupsAsync(currentUserId, "fam", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());

        var handler = Create();

        var result = await handler.Handle(
            new SearchMyGroupsQuery("fam", 999),
            CancellationToken.None);

        result.Should().BeEmpty();

        _groups.Verify(
            x => x.SearchMyGroupsAsync(currentUserId, "fam", 10, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapGroupsToDtos()
    {
        var currentUserId = Guid.NewGuid();

        var first = Group.Create("Family", currentUserId);
        var second = Group.Create("Friends", currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _groups.Setup(x => x.SearchMyGroupsAsync(currentUserId, "f", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var handler = Create();

        var result = await handler.Handle(
            new SearchMyGroupsQuery("f"),
            CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].Id.Should().Be(first.Id);
        result[0].Name.Should().Be("Family");

        result[1].Id.Should().Be(second.Id);
        result[1].Name.Should().Be("Friends");
    }
}