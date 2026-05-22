using InMoment.Application.Features.Users.GetWidget;
using InMoment.Application.Features.Users.SetActiveGroup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Users;

[ApiController]
[Authorize]
[Route("api/users/me")]
public sealed class WidgetController : ControllerBase
{
    private readonly IMediator _mediator;

    public WidgetController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record SetActiveGroupRequest(Guid GroupId);

    // PATCH /api/users/me/active-group
    [HttpPatch("active-group")]
    public async Task<IActionResult> SetActiveGroup(
        [FromBody] SetActiveGroupRequest req,
        CancellationToken ct)
    {
        await _mediator.Send(new SetActiveGroupCommand(req.GroupId), ct);
        return NoContent();
    }

    // GET /api/users/me/widget
    [HttpGet("widget")]
    public async Task<ActionResult<WidgetDto>> GetWidget(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWidgetQuery(), ct);
        return Ok(result);
    }
}