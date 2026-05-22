using InMoment.Application.Features.Uploads.PresignPhotoUpload;
using InMoment.Application.Features.Uploads.PresignSystemAnnouncementMediaUpload;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Uploads;

[ApiController]
[Route("api/uploads")]
[Authorize]
public sealed class UploadsController : ControllerBase
{
    private readonly IMediator _mediator;

    public UploadsController(IMediator mediator) => _mediator = mediator;

    public sealed record PresignPhotoUploadRequest(
        Guid GroupId,
        string ContentType
    );

    [HttpPost("photos/presign")]
    public async Task<ActionResult<PresignPhotoUploadResponse>> PresignPhotoUpload(
        [FromBody] PresignPhotoUploadRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new PresignPhotoUploadCommand(
                request.GroupId,
                request.ContentType),
            ct);

        return Ok(result);
    }

    [HttpPost("system-announcements/presign")]
    public async Task<ActionResult<PresignSystemAnnouncementMediaUploadResponse>> PresignSystemAnnouncementMediaUpload(
        [FromBody] PresignSystemAnnouncementMediaUploadCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}