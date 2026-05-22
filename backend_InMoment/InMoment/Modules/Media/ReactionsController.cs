using InMoment.Application.Features.Media.Reactions.GetSummary;
using InMoment.Application.Features.Media.Reactions.RemoveReaction;
using InMoment.Application.Features.Media.Reactions.SetReaction;
using InMoment.Domain.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Route("api/photos/{photoId:guid}/reactions")]
[Authorize]
public sealed class ReactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReactionsController(IMediator mediator) => _mediator = mediator;

    public sealed record SetReactionRequest(ReactionType Type);

    [HttpPost]
    public async Task<IActionResult> Set(Guid photoId, [FromBody] SetReactionRequest req, CancellationToken ct)
    {
        await _mediator.Send(new SetReactionCommand(photoId, req.Type), ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(Guid photoId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveReactionCommand(photoId), ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<ReactionsSummaryDto>> Get(Guid photoId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReactionsSummaryQuery(photoId), ct);
        return Ok(result);
    }
}