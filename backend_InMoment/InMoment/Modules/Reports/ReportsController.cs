using InMoment.Application.Features.Reports.AppealReport;
using InMoment.Application.Features.Reports.Common;
using InMoment.Application.Features.Reports.CreateReport;
using InMoment.Application.Features.Reports.GetReportDetails;
using InMoment.Application.Features.Reports.ListAllReports;
using InMoment.Application.Features.Reports.ListMyReports;
using InMoment.Application.Features.Reports.ReviewReport;
using InMoment.Domain.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Reports;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private const int DefaultMyLimit = 50;
    private const int MaxMyLimit = 100;
    private const int DefaultAllLimit = 100;
    private const int MaxAllLimit = 200;

    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    public sealed record CreateReportRequest(
        ReportTargetType TargetType,
        Guid TargetId,
        ReportReason Reason,
        string? Description);

    public sealed record ReviewReportRequest(
        ReportStatus Status,
        ReviewReportAction Action = ReviewReportAction.None);

    public sealed record AppealRequest(string Text);

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateReportRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateReportCommand(req.TargetType, req.TargetId, req.Reason, req.Description),
            ct);

        return Ok(id);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> My(
        [FromQuery] int limit = DefaultMyLimit,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultMyLimit, MaxMyLimit);
        var result = await _mediator.Send(new ListMyReportsQuery(safeLimit), ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportListItemDto>>> All(
        [FromQuery] int limit = DefaultAllLimit,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] ReportTargetType? targetType = null,
        [FromQuery] ReportReason? reason = null,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultAllLimit, MaxAllLimit);
        var result = await _mediator.Send(
            new ListAllReportsQuery(safeLimit, status, targetType, reason),
            ct);
        return Ok(result);
    }

    [HttpGet("{reportId:guid}")]
    public async Task<ActionResult<ReportDetailsDto>> Details(
        Guid reportId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReportDetailsQuery(reportId), ct);
        return Ok(result);
    }

    [HttpPatch("{reportId:guid}")]
    public async Task<ActionResult<Guid>> Review(
        Guid reportId,
        [FromBody] ReviewReportRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new ReviewReportCommand(reportId, req.Status, req.Action),
            ct);

        return Ok(id);
    }

    [HttpPost("{reportId:guid}/appeal")]
    public async Task<ActionResult<Guid>> Appeal(
        Guid reportId,
        [FromBody] AppealRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new AppealReportCommand(reportId, req.Text),
            ct);

        return Ok(id);
    }

    private static int NormalizeLimit(int value, int defaultValue, int maxValue)
    {
        if (value <= 0)
            return defaultValue;

        return value > maxValue ? maxValue : value;
    }
}