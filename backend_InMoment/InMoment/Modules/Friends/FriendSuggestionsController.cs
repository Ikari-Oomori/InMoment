using InMoment.Application.Features.Friends.Suggestions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Friends;

[ApiController]
[Authorize]
[Route("api/friends/suggestions")]
public sealed class FriendSuggestionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FriendSuggestionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendSuggestionDto>>> Get(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var safeQuery = (q ?? string.Empty).Trim();
        var safeLimit = limit is < 1 or > 20 ? 10 : limit;

        var result = await _mediator.Send(
            new SearchFriendSuggestionsQuery(safeQuery, safeLimit),
            ct);

        return Ok(result);
    }
}