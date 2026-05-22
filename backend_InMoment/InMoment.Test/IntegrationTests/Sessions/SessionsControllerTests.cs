using FluentAssertions;
using InMoment.API.Modules.Sessions;
using InMoment.Application.Features.Sessions.List;
using InMoment.Application.Features.Sessions.Revoke;
using InMoment.Application.Features.Sessions.RevokeOthers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Sessions;

public sealed class SessionsControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private SessionsController CreateController(string? refreshTokenHeader = null)
    {
        var controller = new SessionsController(_mediator.Object);

        var httpContext = new DefaultHttpContext();
        if (refreshTokenHeader is not null)
        {
            httpContext.Request.Headers["X-Refresh-Token"] = refreshTokenHeader;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    [Fact]
    public async Task Get_ShouldPassRefreshTokenFromHeader_ToQuery()
    {
        var expected = new List<SessionDto>
        {
            new(
                Guid.NewGuid(),
                "iPhone 15",
                "iOS",
                "127.0.0.1",
                "Mozilla/5.0",
                null,
                null,
                null,
                DateTime.UtcNow.AddMinutes(-30),
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddDays(20),
                false,
                false)
        };

        _mediator.Setup(x => x.Send(
                It.Is<ListSessionsQuery>(q => q.CurrentRefreshToken == "refresh-token-123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController("refresh-token-123");

        var actionResult = await controller.Get(CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);

        _mediator.Verify(x => x.Send(
            It.Is<ListSessionsQuery>(q => q.CurrentRefreshToken == "refresh-token-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_ShouldPassNullRefreshToken_WhenHeaderMissing()
    {
        var expected = Array.Empty<SessionDto>();

        _mediator.Setup(x => x.Send(
                It.Is<ListSessionsQuery>(q => q.CurrentRefreshToken == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController();

        var actionResult = await controller.Get(CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);

        _mediator.Verify(x => x.Send(
            It.Is<ListSessionsQuery>(q => q.CurrentRefreshToken == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_ShouldSendCommand_AndReturnNoContent()
    {
        var sessionId = Guid.NewGuid();

        _mediator.Setup(x => x.Send(
                It.Is<RevokeSessionCommand>(c => c.SessionId == sessionId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController();

        var result = await controller.Revoke(sessionId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mediator.Verify(x => x.Send(
            It.Is<RevokeSessionCommand>(c => c.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeOthers_ShouldPassRefreshToken_AndReturnOk()
    {
        _mediator.Setup(x => x.Send(
                It.Is<RevokeOtherSessionsCommand>(c => c.CurrentRefreshToken == "refresh-token-123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var controller = CreateController("refresh-token-123");

        var result = await controller.RevokeOthers(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();

        _mediator.Verify(x => x.Send(
            It.Is<RevokeOtherSessionsCommand>(c => c.CurrentRefreshToken == "refresh-token-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}