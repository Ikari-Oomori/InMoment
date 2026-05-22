using InMoment.Application.Features.Privacy.Common;
using InMoment.Application.Features.Privacy.GetPrivacy;
using InMoment.Application.Features.Privacy.UpdatePrivacy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Privacy;

[ApiController]
[Authorize]
[Route("api/privacy")]
public sealed class PrivacyController : ControllerBase
{
    private readonly IMediator _mediator;

    public PrivacyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PrivacySettingsDto>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPrivacyQuery(), ct);
        return Ok(result);
    }

    [HttpPatch]
    public async Task<IActionResult> Update(
        [FromBody] UpdatePrivacyRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new UpdatePrivacyCommand(
            request.AllowFriendRequestsFrom,
            request.AllowGroupInvitesFrom,
            request.DiscoverableByContacts,
            request.DiscoverableBySearch), ct);

        return NoContent();
    }
}