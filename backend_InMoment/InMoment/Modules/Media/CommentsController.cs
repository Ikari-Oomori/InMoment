using InMoment.Application.Features.Media.Comments.CreateReply;
using InMoment.Application.Features.Media.Comments.CreateRoot;
using InMoment.Application.Features.Media.Comments.Delete;
using InMoment.Application.Features.Media.Comments.Edit;
using InMoment.Application.Features.Media.Comments.List;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CommentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommentsController(IMediator mediator) => _mediator = mediator;

    public sealed record CreateCommentRequest(string? Text, string? GifUrl);
    public sealed record CreateReplyRequest(Guid ParentCommentId, string? Text, string? GifUrl);
    public sealed record EditCommentRequest(string Text);

    [HttpPost("photos/{photoId:guid}/comments")]
    public async Task<ActionResult<Guid>> Create(
        Guid photoId,
        [FromBody] CreateCommentRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateRootCommentCommand(photoId, req.Text, req.GifUrl),
            ct);

        return Ok(id);
    }

    [HttpPost("photos/{photoId:guid}/comments/reply")]
    public async Task<ActionResult<Guid>> Reply(
        Guid photoId,
        [FromBody] CreateReplyRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateReplyCommentCommand(photoId, req.ParentCommentId, req.Text, req.GifUrl),
            ct);

        return Ok(id);
    }

    [Obsolete("For mobile client use GET /api/photos/{photoId}/comments/paged.")]
    [HttpGet("photos/{photoId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> List(
        Guid photoId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var safeLimit = limit <= 0 ? 50 : limit;
        var result = await _mediator.Send(new ListCommentsQuery(photoId, safeLimit), ct);
        return Ok(result);
    }

    [HttpPatch("comments/{commentId:guid}")]
    public async Task<ActionResult<Guid>> Edit(
        Guid commentId,
        [FromBody] EditCommentRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(new EditCommentCommand(commentId, req.Text), ct);
        return Ok(id);
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid commentId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteCommentCommand(commentId), ct);
        return NoContent();
    }
}