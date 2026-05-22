using FluentAssertions;
using InMoment.API.Modules.Accounts;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Application.Features.Accounts.DeactivateMyAccount;
using InMoment.Application.Features.Accounts.GetMyDataSummary;
using InMoment.Application.Features.Accounts.PermanentlyDeleteMyAccount;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Accounts;

public sealed class AccountControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private AccountController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task GetDataSummary_ShouldSendQuery_AndReturnOk()
    {
        var dto = new AccountDataSummaryDto(
            Guid.NewGuid(),
            true,
            2,
            1,
            5,
            8,
            13,
            3,
            2);

        _mediator.Setup(x =>
                x.Send(
                    It.IsAny<GetMyDataSummaryQuery>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = Create();

        var result = await controller.GetDataSummary(
            CancellationToken.None);

        var ok = result.Result
            .Should()
            .BeOfType<OkObjectResult>()
            .Subject;

        ok.Value.Should().Be(dto);

        _mediator.Verify(
            x => x.Send(
                It.Is<GetMyDataSummaryQuery>(_ => true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Deactivate_ShouldSendCommand_AndReturnNoContent()
    {
        var controller = Create();

        var result = await controller.Deactivate(
            CancellationToken.None);

        result.Should()
            .BeOfType<NoContentResult>();

        _mediator.Verify(
            x => x.Send(
                It.Is<DeactivateMyAccountCommand>(_ => true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PermanentDelete_ShouldSendCommand_AndReturnNoContent()
    {
        var controller = Create();

        var request =
            new AccountController.PermanentDeleteAccountRequest(
                "DELETE");

        var result = await controller.PermanentDelete(
            request,
            CancellationToken.None);

        result.Should()
            .BeOfType<NoContentResult>();

        _mediator.Verify(
            x => x.Send(
                It.Is<PermanentlyDeleteMyAccountCommand>(
                    c => c.Confirmation == "DELETE"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}