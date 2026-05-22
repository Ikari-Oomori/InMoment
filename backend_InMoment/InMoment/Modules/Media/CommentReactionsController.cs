using InMoment.Application.Features.Media.CommentReactions.GetSummary;
using InMoment.Application.Features.Media.CommentReactions.RemoveReaction;
using InMoment.Application.Features.Media.CommentReactions.SetReaction;
using InMoment.Domain.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api/comments/{commentId:guid}/reactions")]
public sealed class CommentReactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommentReactionsController(IMediator mediator) => _mediator = mediator;

    public sealed record SetCommentReactionRequest(ReactionType Type);

    [HttpPost]
    public async Task<IActionResult> Set(
        Guid commentId,
        [FromBody] SetCommentReactionRequest req,
        CancellationToken ct)
    {
        await _mediator.Send(new SetCommentReactionCommand(commentId, req.Type), ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(Guid commentId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveCommentReactionCommand(commentId), ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<CommentReactionsSummaryDto>> Get(Guid commentId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCommentReactionsSummaryQuery(commentId), ct);
        return Ok(result);
    }
}