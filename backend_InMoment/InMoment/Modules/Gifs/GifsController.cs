using InMoment.Application.Abstractions.Gifs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Gifs;

[ApiController]
[Authorize]
[Route("api/gifs")]
public sealed class GifsController : ControllerBase
{
    private readonly IGifSearchService _gifSearchService;

    public GifsController(IGifSearchService gifSearchService)
    {
        _gifSearchService = gifSearchService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<GifSearchItemDto>>> Search(
        [FromQuery] string? query,
        [FromQuery] int limit = 18,
        CancellationToken ct = default)
    {
        var result = await _gifSearchService.SearchAsync(
            query ?? string.Empty,
            limit,
            ct);

        return Ok(result);
    }
}