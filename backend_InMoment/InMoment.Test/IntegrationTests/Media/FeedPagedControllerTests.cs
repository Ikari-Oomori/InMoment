using FluentAssertions;
using InMoment.API.Modules.Media;
using InMoment.Application.Features.Media.GetGroupFeed;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Media;

public sealed class FeedPagedControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private FeedPagedController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task GetPaged_ShouldReturnOk_WithMediatorResult()
    {
        var groupId = Guid.NewGuid();
        var expected = new FeedPageDto(
            Array.Empty<GroupFeedItemDto>(),
            "next-cursor");

        _mediator.Setup(x => x.Send(
                It.Is<GetGroupFeedPageQuery>(q =>
                    q.GroupId == groupId &&
                    q.Limit == 10 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(groupId, 10, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldUseDefaultLimit_WhenLimitLessThanOrEqualToZero()
    {
        var groupId = Guid.NewGuid();
        var expected = new FeedPageDto(
            Array.Empty<GroupFeedItemDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<GetGroupFeedPageQuery>(q =>
                    q.GroupId == groupId &&
                    q.Limit == 20 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(groupId, 0, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldCapLimit_WhenGreaterThanMaxLimit()
    {
        var groupId = Guid.NewGuid();
        var expected = new FeedPageDto(
            Array.Empty<GroupFeedItemDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<GetGroupFeedPageQuery>(q =>
                    q.GroupId == groupId &&
                    q.Limit == 50 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(groupId, 999, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldTrimCursor()
    {
        var groupId = Guid.NewGuid();
        var expected = new FeedPageDto(
            Array.Empty<GroupFeedItemDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<GetGroupFeedPageQuery>(q =>
                    q.GroupId == groupId &&
                    q.Limit == 20 &&
                    q.Cursor == "trimmed-cursor"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(groupId, 20, "  trimmed-cursor  ", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldPassNullCursor_WhenCursorBlank()
    {
        var groupId = Guid.NewGuid();
        var expected = new FeedPageDto(
            Array.Empty<GroupFeedItemDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<GetGroupFeedPageQuery>(q =>
                    q.GroupId == groupId &&
                    q.Limit == 20 &&
                    q.Cursor == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(groupId, 20, "   ", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }
}