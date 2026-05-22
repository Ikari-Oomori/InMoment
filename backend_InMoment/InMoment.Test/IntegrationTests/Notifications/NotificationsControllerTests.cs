using FluentAssertions;
using InMoment.API.Modules.Notifications;
using InMoment.Application.Features.Notifications.GetUnreadCount;
using InMoment.Application.Features.Notifications.List;
using InMoment.Application.Features.Notifications.MarkAllRead;
using InMoment.Application.Features.Notifications.MarkRead;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Notifications;

public sealed class NotificationsControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private NotificationsController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task List_ShouldReturnOk_WithMediatorResult()
    {
        var expected = new NotificationsPageDto(
            Array.Empty<NotificationDto>(),
            "next-cursor",
            3);

        _mediator.Setup(x => x.Send(
                It.Is<ListNotificationsQuery>(q =>
                    q.Limit == 10 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.List(10, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task List_ShouldUseDefaultLimit_WhenLimitLessThanOrEqualToZero()
    {
        var expected = new NotificationsPageDto(
            Array.Empty<NotificationDto>(),
            null,
            0);

        _mediator.Setup(x => x.Send(
                It.Is<ListNotificationsQuery>(q =>
                    q.Limit == 20 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.List(0, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task List_ShouldCapLimit_WhenGreaterThanMaxLimit()
    {
        var expected = new NotificationsPageDto(
            Array.Empty<NotificationDto>(),
            null,
            0);

        _mediator.Setup(x => x.Send(
                It.Is<ListNotificationsQuery>(q =>
                    q.Limit == 100 &&
                    q.Cursor == "cursor-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.List(999, "cursor-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task List_ShouldTrimCursor()
    {
        var expected = new NotificationsPageDto(
            Array.Empty<NotificationDto>(),
            null,
            0);

        _mediator.Setup(x => x.Send(
                It.Is<ListNotificationsQuery>(q =>
                    q.Limit == 20 &&
                    q.Cursor == "trimmed-cursor"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.List(20, "  trimmed-cursor  ", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task List_ShouldPassNullCursor_WhenCursorBlank()
    {
        var expected = new NotificationsPageDto(
            Array.Empty<NotificationDto>(),
            null,
            0);

        _mediator.Setup(x => x.Send(
                It.Is<ListNotificationsQuery>(q =>
                    q.Limit == 20 &&
                    q.Cursor == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.List(20, "   ", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetUnreadCount_ShouldReturnOk_WithMediatorResult()
    {
        var expected = new UnreadNotificationsCountDto(7);

        _mediator.Setup(x => x.Send(
                It.IsAny<GetUnreadNotificationsCountQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.GetUnreadCount(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task MarkRead_ShouldSendCommand_AndReturnNoContent()
    {
        var notificationId = Guid.NewGuid();

        _mediator.Setup(x => x.Send(
                It.Is<MarkNotificationReadCommand>(c => c.NotificationId == notificationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        var controller = Create();

        var result = await controller.MarkRead(notificationId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAllRead_ShouldSendCommand_AndReturnNoContent()
    {
        _mediator.Setup(x => x.Send(
                It.IsAny<MarkAllNotificationsReadCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        var controller = Create();

        var result = await controller.MarkAllRead(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }
}