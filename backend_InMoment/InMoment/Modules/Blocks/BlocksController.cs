using InMoment.Application.Features.Blocks.BlockUser;
using InMoment.Application.Features.Blocks.Common;
using InMoment.Application.Features.Blocks.ListBlocked;
using InMoment.Application.Features.Blocks.UnblockUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Blocks;

[ApiController]
[Authorize]
[Route("api/blocks")]
public sealed class BlocksController : ControllerBase
{
    private readonly IMediator _mediator;

    public BlocksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlockedUserDto>>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListBlockedUsersQuery(), ct);
        return Ok(result);
    }

    [HttpPost("{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new BlockUserCommand(userId), ct);
        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new UnblockUserCommand(userId), ct);
        return NoContent();
    }
}