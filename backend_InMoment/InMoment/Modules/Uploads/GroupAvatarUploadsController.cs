using InMoment.Application.Features.Uploads.PresignGroupAvatarUpload;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Uploads;

[ApiController]
[Authorize]
[Route("api/uploads")]
public sealed class GroupAvatarUploadsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GroupAvatarUploadsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record PresignGroupAvatarUploadRequest(Guid GroupId, string ContentType);

    // POST /api/uploads/group-avatar/presign
    [HttpPost("group-avatar/presign")]
    public async Task<ActionResult<PresignGroupAvatarUploadResponse>> Presign(
        [FromBody] PresignGroupAvatarUploadRequest req,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new PresignGroupAvatarUploadCommand(req.GroupId, req.ContentType), ct);

        return Ok(result);
    }
}