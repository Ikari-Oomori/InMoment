using InMoment.Application.Features.Search.Mentions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Search;

[ApiController]
[Authorize]
[Route("api/mentions")]
public sealed class MentionController : ControllerBase
{
    private const int DefaultLimit = 5;
    private const int MaxLimit = 20;

    private readonly IMediator _mediator;

    public MentionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<MentionUserDto>>> SearchMentionUsers(
        [FromQuery] string q,
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] Guid? groupId = null,
        CancellationToken ct = default)
    {
        var safeQuery = NormalizeQuery(q);
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);

        var result = await _mediator.Send(
            new MentionUsersQuery(safeQuery, safeLimit, groupId),
            ct);

        return Ok(result);
    }

    private static string NormalizeQuery(string? value)
        => (value ?? string.Empty).Trim();

    private static int NormalizeLimit(int value, int defaultValue, int maxValue)
    {
        if (value <= 0)
            return defaultValue;

        return value > maxValue ? maxValue : value;
    }
}