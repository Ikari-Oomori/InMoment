using InMoment.Application.Features.Media.DeletePhoto;
using InMoment.Application.Features.Media.EditPhotoCaption;
using InMoment.Application.Features.Media.PublishPhoto;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Route("api/groups/{groupId:guid}/photos")]
[Authorize]
public sealed class PhotosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhotosController(IMediator mediator) => _mediator = mediator;

    public sealed record PublishPhotoRequest(
        string StorageKey,
        string ContentType,
        long SizeBytes,
        string? Caption,
        long? TrimStartMs,
        long? TrimEndMs
    );

    public sealed record EditPhotoRequest(
        string? Caption
    );

    // POST /api/groups/{groupId}/photos
    [HttpPost]
    public async Task<ActionResult<Guid>> Publish(
        Guid groupId,
        [FromBody] PublishPhotoRequest request,
        CancellationToken ct)
    {
        var photoId = await _mediator.Send(
            new PublishPhotoCommand(
                groupId,
                request.StorageKey,
                request.ContentType,
                request.SizeBytes,
                request.Caption,
                request.TrimStartMs,
                request.TrimEndMs),
            ct);

        return Ok(photoId);
    }

    // PATCH /api/groups/{groupId}/photos/{photoId}
    [HttpPatch("{photoId:guid}")]
    public async Task<ActionResult<Guid>> Edit(
        Guid groupId,
        Guid photoId,
        [FromBody] EditPhotoRequest request,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new EditPhotoCaptionCommand(groupId, photoId, request.Caption),
            ct);

        return Ok(id);
    }

    // DELETE /api/groups/{groupId}/photos/{photoId}
    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> Delete(
        Guid groupId,
        Guid photoId,
        CancellationToken ct)
    {
        await _mediator.Send(new DeletePhotoCommand(groupId, photoId), ct);
        return NoContent();
    }
}