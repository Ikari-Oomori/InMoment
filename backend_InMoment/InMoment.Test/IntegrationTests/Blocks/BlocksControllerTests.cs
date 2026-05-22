using FluentAssertions;
using InMoment.API.Modules.Blocks;
using InMoment.Application.Features.Blocks.BlockUser;
using InMoment.Application.Features.Blocks.Common;
using InMoment.Application.Features.Blocks.ListBlocked;
using InMoment.Application.Features.Blocks.UnblockUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Blocks;

public sealed class BlocksControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private BlocksController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task Get_ShouldSendQuery_AndReturnOk()
    {
        var blockedAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

        IReadOnlyList<BlockedUserDto> dto = new[]
        {
            new BlockedUserDto(
                Guid.NewGuid(),
                "blocked_user",
                "Blocked",
                "User",
                "https://cdn.example.com/u.jpg",
                blockedAt)
        };

        _mediator.Setup(x => x.Send(It.IsAny<ListBlockedUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = Create();

        var result = await controller.Get(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);

        _mediator.Verify(
            x => x.Send(It.Is<ListBlockedUsersQuery>(_ => true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Block_ShouldSendCommand_AndReturnNoContent()
    {
        var userId = Guid.NewGuid();
        var controller = Create();

        var result = await controller.Block(userId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mediator.Verify(
            x => x.Send(
                It.Is<BlockUserCommand>(c => c.BlockedUserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Unblock_ShouldSendCommand_AndReturnNoContent()
    {
        var userId = Guid.NewGuid();
        var controller = Create();

        var result = await controller.Unblock(userId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        _mediator.Verify(
            x => x.Send(
                It.Is<UnblockUserCommand>(c => c.BlockedUserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}