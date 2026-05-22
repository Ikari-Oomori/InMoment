using InMoment.Application.Features.Memories.GetPersonalCalendar;
using InMoment.Application.Features.Memories.GetPersonalMemoriesByDate;
using InMoment.Application.Features.Memories.GetPersonalStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Memories;

[ApiController]
[Authorize]
[Route("api/memories/personal")]
public sealed class PersonalMemoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PersonalMemoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/memories/personal/calendar?year=2026&month=4
    [HttpGet("calendar")]
    public async Task<ActionResult<PersonalCalendarDto>> GetCalendar(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPersonalCalendarQuery(year, month), ct);

        return Ok(result);
    }

    // GET /api/memories/personal?date=2026-04-01
    [HttpGet]
    public async Task<ActionResult<PersonalMemoriesByDateDto>> GetByDate(
        [FromQuery] DateOnly date,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPersonalMemoriesByDateQuery(date), ct);

        return Ok(result);
    }

    // GET /api/memories/personal/stats
    [HttpGet("stats")]
    public async Task<ActionResult<PersonalMemoriesStatsDto>> GetStats(
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPersonalMemoriesStatsQuery(), ct);

        return Ok(result);
    }
}