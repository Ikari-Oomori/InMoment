using InMoment.Application.Features.Invitations.AcceptInvitation;
using InMoment.Application.Features.Invitations.MyInvitations;
using InMoment.Application.Features.Invitations.RejectInvitation;
using InMoment.Application.Features.Invitations.CancelInvitation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Invitations;

[ApiController]
[Route("api/invitations")]
[Authorize]
public sealed class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvitationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> My(CancellationToken ct)
    {
        var result = await _mediator.Send(new MyInvitationsQuery(), ct);
        return Ok(result);
    }

    [HttpPost("{invitationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid invitationId, CancellationToken ct)
    {
        await _mediator.Send(new AcceptInvitationCommand(invitationId), ct);
        return NoContent();
    }

    [HttpPost("{invitationId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid invitationId, CancellationToken ct)
    {
        await _mediator.Send(new RejectInvitationCommand(invitationId), ct);
        return NoContent();
    }

    [HttpPost("{invitationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid invitationId, CancellationToken ct)
    {
        await _mediator.Send(new CancelInvitationCommand(invitationId), ct);
        return NoContent();
    }
}