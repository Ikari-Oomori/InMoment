using InMoment.Application.Features.Media.GetGroupFeed;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/feed")]
public sealed class FeedPagedController : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    private readonly IMediator _mediator;

    public FeedPagedController(IMediator mediator) => _mediator = mediator;

    // GET /api/groups/{groupId}/feed/paged?limit=20&cursor=...
    [HttpGet("paged")]
    public async Task<ActionResult<FeedPageDto>> GetPaged(
        Guid groupId,
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);
        var safeCursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        var res = await _mediator.Send(
            new GetGroupFeedPageQuery(groupId, safeLimit, safeCursor),
            ct);

        return Ok(res);
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