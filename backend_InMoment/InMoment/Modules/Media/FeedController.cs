using InMoment.Application.Features.Media.GetGroupFeed;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Media;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/feed")]
public sealed class FeedController : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    private readonly IMediator _mediator;

    public FeedController(IMediator mediator) => _mediator = mediator;

    // GET /api/groups/{groupId}/feed?limit=20
    // Legacy/simple endpoint. For mobile client use /api/groups/{groupId}/feed/paged.
    [Obsolete("For mobile client use GET /api/groups/{groupId}/feed/paged.")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupFeedItemDto>>> Get(
        Guid groupId,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);

        var res = await _mediator.Send(
            new GetGroupFeedQuery(groupId, safeLimit),
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