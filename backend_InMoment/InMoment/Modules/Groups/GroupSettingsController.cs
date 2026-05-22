using InMoment.Application.Features.Groups.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Groups;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}")]
public sealed class GroupSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GroupSettingsController(IMediator mediator) => _mediator = mediator;

    public sealed record UpdateGroupSettingsRequest(string Name, string? Description);
    public sealed record SetGroupAvatarRequest(string? AvatarUrl);

    // GET /api/groups/{groupId}/settings
    [HttpGet("settings")]
    public async Task<ActionResult<GroupSettingsDto>> Get(Guid groupId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGroupSettingsQuery(groupId), ct);
        return Ok(result);
    }

    // PATCH /api/groups/{groupId}/settings
    [HttpPatch("settings")]
    public async Task<ActionResult<GroupSettingsDto>> Update(
        Guid groupId,
        [FromBody] UpdateGroupSettingsRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateGroupSettingsCommand(groupId, req.Name, req.Description), ct);

        return Ok(result);
    }

    // POST /api/groups/{groupId}/avatar
    [HttpPost("avatar")]
    public async Task<IActionResult> SetAvatar(
        Guid groupId,
        [FromBody] SetGroupAvatarRequest req,
        CancellationToken ct)
    {
        await _mediator.Send(new SetGroupAvatarCommand(groupId, req.AvatarUrl), ct);
        return NoContent();
    }
}