using InMoment.Application.Features.Search.Groups;
using InMoment.Application.Features.Search.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Search;

[ApiController]
[Authorize]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly IMediator _mediator;

    public SearchController(IMediator mediator) => _mediator = mediator;

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<SearchUserDto>>> SearchUsers(
        [FromQuery] string q,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken ct = default)
    {
        var safeQuery = NormalizeQuery(q);
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);

        var result = await _mediator.Send(
            new SearchUsersQuery(safeQuery, safeLimit),
            ct);

        return Ok(result);
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<SearchGroupDto>>> SearchGroups(
        [FromQuery] string q,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken ct = default)
    {
        var safeQuery = NormalizeQuery(q);
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);

        var result = await _mediator.Send(
            new SearchMyGroupsQuery(safeQuery, safeLimit),
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