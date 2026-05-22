using InMoment.Application.Features.Discussions.ListGroupDiscussions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Discussions;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}/discussions")]
public sealed class DiscussionsController : ControllerBase
{
    private const int DefaultLimit = 30;
    private const int MaxLimit = 100;

    private readonly IMediator _mediator;

    public DiscussionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/groups/{groupId}/discussions?limit=30
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupDiscussionDto>>> Get(
        Guid groupId,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);

        var result = await _mediator.Send(
            new ListGroupDiscussionsQuery(groupId, safeLimit),
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