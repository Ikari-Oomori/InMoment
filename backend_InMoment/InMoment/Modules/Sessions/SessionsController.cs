using InMoment.Application.Features.Sessions.List;
using InMoment.Application.Features.Sessions.Revoke;
using InMoment.Application.Features.Sessions.RevokeOthers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Sessions;

[ApiController]
[Authorize]
[Route("api/sessions")]
public sealed class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> Get(CancellationToken ct)
    {
        var refreshToken = Request.Headers["X-Refresh-Token"].FirstOrDefault();

        var result = await _mediator.Send(
            new ListSessionsQuery(refreshToken), ct);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RevokeSessionCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("others")]
    public async Task<IActionResult> RevokeOthers(CancellationToken ct)
    {
        var refreshToken = Request.Headers["X-Refresh-Token"].FirstOrDefault();

        var revokedCount = await _mediator.Send(
            new RevokeOtherSessionsCommand(refreshToken),
            ct);

        return Ok(new { revokedCount });
    }
}