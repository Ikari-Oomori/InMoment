using InMoment.Application.Features.Uploads.PresignProfilePhotoUpload;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Uploads;

[ApiController]
[Authorize]
[Route("api/uploads")]
public sealed class ProfilePhotoUploadsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProfilePhotoUploadsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("profile-photo/presign")]
    public async Task<ActionResult<PresignProfilePhotoUploadResponse>> Presign(
        [FromBody] PresignProfilePhotoUploadRequest req,
        CancellationToken ct)
        => Ok(await _mediator.Send(new PresignProfilePhotoUploadCommand(req.ContentType), ct));
}

public sealed record PresignProfilePhotoUploadRequest(string ContentType);