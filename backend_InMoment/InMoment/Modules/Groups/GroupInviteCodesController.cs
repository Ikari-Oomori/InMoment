using InMoment.Application.Features.Groups.InviteCodes.Create;
using InMoment.Application.Features.Groups.InviteCodes.Join;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Groups;

[ApiController]
[Authorize]
[Route("api/groups")]
public sealed class GroupInviteCodesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GroupInviteCodesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("{groupId:guid}/invite-code")]
    public async Task<ActionResult<string>> Create(
        Guid groupId,
        [FromBody] CreateInviteCodeCommand body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            body with { GroupId = groupId }, ct);

        return Ok(result);
    }

    [HttpPost("join-by-code")]
    public async Task<IActionResult> Join(
        [FromBody] JoinByCodeCommand cmd,
        CancellationToken ct)
    {
        await _mediator.Send(cmd, ct);
        return Ok();
    }
}