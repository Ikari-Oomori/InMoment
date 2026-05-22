using InMoment.Application.Features.SystemAnnouncements.Create;
using InMoment.Application.Features.SystemAnnouncements.List;
using InMoment.Application.Features.SystemAnnouncements.Update;
using InMoment.Application.Features.SystemAnnouncements.Delete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.SystemAnnouncements;

[ApiController]
[Authorize]
[Route("api/system-announcements")]
public sealed class SystemAnnouncementsController : ControllerBase
{
    private readonly CreateSystemAnnouncementHandler _create;
    private readonly UpdateSystemAnnouncementHandler _update;
    private readonly ListSystemAnnouncementsHandler _list;
    private readonly DeleteSystemAnnouncementHandler _delete;

    public SystemAnnouncementsController(
        CreateSystemAnnouncementHandler create,
        UpdateSystemAnnouncementHandler update,
        ListSystemAnnouncementsHandler list,
        DeleteSystemAnnouncementHandler delete)
    {
        _create = create;
        _update = update;
        _list = list;
        _delete = delete;
    }

    public sealed record CreateAnnouncementRequest(
        string Text,
        string? MediaUrl,
        string? MediaContentType);

    public sealed record UpdateAnnouncementRequest(
        string Text,
        string? MediaUrl,
        string? MediaContentType);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemAnnouncementDto>>> List(
        [FromQuery] int limit,
        CancellationToken ct)
    {
        return Ok(await _list.Handle(limit, ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken ct)
    {
        var id = await _create.Handle(
            request.Text,
            request.MediaUrl,
            request.MediaContentType,
            ct);

        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAnnouncementRequest request,
        CancellationToken ct)
    {
        await _update.Handle(
            id,
            request.Text,
            request.MediaUrl,
            request.MediaContentType,
            ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _delete.Handle(id, ct);
        return NoContent();
    }
}