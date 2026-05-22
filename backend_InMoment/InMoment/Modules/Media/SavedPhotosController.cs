using InMoment.Application.Features.Media.Saved.ListSavedPhotos;
using InMoment.Application.Features.Media.Saved.SavePhoto;
using InMoment.Application.Features.Media.Saved.UnsavePhoto;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api")]
public sealed class SavedPhotosController : ControllerBase
{
    private readonly IMediator _mediator;

    public SavedPhotosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("photos/{photoId:guid}/save")]
    public async Task<IActionResult> Save(Guid photoId, CancellationToken ct)
    {
        await _mediator.Send(new SavePhotoCommand(photoId), ct);
        return NoContent();
    }

    [HttpDelete("photos/{photoId:guid}/save")]
    public async Task<IActionResult> Unsave(Guid photoId, CancellationToken ct)
    {
        await _mediator.Send(new UnsavePhotoCommand(photoId), ct);
        return NoContent();
    }

    [HttpGet("photos/saved")]
    public async Task<ActionResult<SavedPhotosPageDto>> GetSaved(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var safeLimit = limit is < 1 or > 50 ? 20 : limit;
        var safeCursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        var result = await _mediator.Send(
            new ListSavedPhotosQuery(safeLimit, safeCursor),
            ct);

        return Ok(result);
    }
}