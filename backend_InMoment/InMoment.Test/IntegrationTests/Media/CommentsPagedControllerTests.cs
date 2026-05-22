using FluentAssertions;
using InMoment.API.Modules.Media;
using InMoment.Application.Features.Media.Comments.ListPaged;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Media;

public sealed class CommentsPagedControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private CommentsPagedController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task GetPaged_ShouldReturnOk_WithMediatorResult()
    {
        var photoId = Guid.NewGuid();
        var expected = new CommentsPageDto(
            Array.Empty<PagedCommentDto>(),
            "next-cursor");

        _mediator.Setup(x => x.Send(
                It.Is<ListCommentsPageQuery>(q =>
                    q.PhotoId == photoId &&
                    q.Limit == 10 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(photoId, 10, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldUseDefaultLimit_WhenLimitLessThanOrEqualToZero()
    {
        var photoId = Guid.NewGuid();
        var expected = new CommentsPageDto(
            Array.Empty<PagedCommentDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<ListCommentsPageQuery>(q =>
                    q.PhotoId == photoId &&
                    q.Limit == 20 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(photoId, 0, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldCapLimit_WhenGreaterThanMaxLimit()
    {
        var photoId = Guid.NewGuid();
        var expected = new CommentsPageDto(
            Array.Empty<PagedCommentDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<ListCommentsPageQuery>(q =>
                    q.PhotoId == photoId &&
                    q.Limit == 50 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(photoId, 999, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldTrimCursor()
    {
        var photoId = Guid.NewGuid();
        var expected = new CommentsPageDto(
            Array.Empty<PagedCommentDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<ListCommentsPageQuery>(q =>
                    q.PhotoId == photoId &&
                    q.Limit == 20 &&
                    q.Cursor == "trimmed-cursor"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(photoId, 20, "  trimmed-cursor  ", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPaged_ShouldPassNullCursor_WhenCursorBlank()
    {
        var photoId = Guid.NewGuid();
        var expected = new CommentsPageDto(
            Array.Empty<PagedCommentDto>(),
            null);

        _mediator.Setup(x => x.Send(
                It.Is<ListCommentsPageQuery>(q =>
                    q.PhotoId == photoId &&
                    q.Limit == 20 &&
                    q.Cursor == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetPaged(photoId, 20, "   ", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }
}