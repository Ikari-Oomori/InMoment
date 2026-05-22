using InMoment.Application.Features.Media.GetPhotoDetails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api/photos/{photoId:guid}")]
public sealed class PhotoDetailsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhotoDetailsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PhotoDetailsDto>> Get(
        Guid photoId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetPhotoDetailsQuery(photoId),
            ct);

        return Ok(result);
    }
}