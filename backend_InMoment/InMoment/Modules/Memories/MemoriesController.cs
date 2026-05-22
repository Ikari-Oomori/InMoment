using InMoment.Application.Features.Memories.GetGroupCalendar;
using InMoment.Application.Features.Memories.GetGroupMemoriesByDate;
using InMoment.Application.Features.Memories.GetGroupStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Memories;

[ApiController]
[Authorize]
[Route("api/groups/{groupId:guid}")]
public sealed class MemoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MemoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/groups/{groupId}/calendar?year=2026&month=3
    [HttpGet("calendar")]
    public async Task<ActionResult<GroupCalendarDto>> GetCalendar(
        Guid groupId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetGroupCalendarQuery(groupId, year, month), ct);

        return Ok(result);
    }

    // GET /api/groups/{groupId}/memories?date=2026-03-07
    [HttpGet("memories")]
    public async Task<ActionResult<GroupMemoriesByDateDto>> GetMemoriesByDate(
        Guid groupId,
        [FromQuery] DateOnly date,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetGroupMemoriesByDateQuery(groupId, date), ct);

        return Ok(result);
    }

    // GET /api/groups/{groupId}/memories/stats
    [HttpGet("memories/stats")]
    public async Task<ActionResult<GroupMemoriesStatsDto>> GetStats(
        Guid groupId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetGroupMemoriesStatsQuery(groupId), ct);

        return Ok(result);
    }
}