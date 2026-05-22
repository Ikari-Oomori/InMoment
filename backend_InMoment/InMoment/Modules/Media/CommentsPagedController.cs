using InMoment.Application.Features.Media.Comments.ListPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api/photos/{photoId:guid}/comments")]
public sealed class CommentsPagedController : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    private readonly IMediator _mediator;

    public CommentsPagedController(IMediator mediator) => _mediator = mediator;

    // GET /api/photos/{photoId}/comments/paged?limit=20&cursor=...
    [HttpGet("paged")]
    public async Task<ActionResult<CommentsPageDto>> GetPaged(
        Guid photoId,
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);
        var safeCursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        var result = await _mediator.Send(
            new ListCommentsPageQuery(photoId, safeLimit, safeCursor),
            ct);

        return Ok(result);
    }

    private static int NormalizeLimit(int value, int defaultValue, int maxValue)
    {
        if (value <= 0)
            return defaultValue;

        return value > maxValue
            ? maxValue
            : value;
    }
}