using InMoment.Application.Features.Accounts.Common;
using InMoment.Application.Features.Accounts.CreateMyDeletionRequest;
using InMoment.Application.Features.Accounts.DeactivateMyAccount;
using InMoment.Application.Features.Accounts.GetDeletionRequestDetails;
using InMoment.Application.Features.Accounts.GetMyDataSummary;
using InMoment.Application.Features.Accounts.GetMyDeletionRequest;
using InMoment.Application.Features.Accounts.ListDeletionRequests;
using InMoment.Application.Features.Accounts.PermanentlyDeleteMyAccount;
using InMoment.Application.Features.Accounts.ReviewDeletionRequest;
using InMoment.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Accounts;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record PermanentDeleteAccountRequest(string Confirmation);
    public sealed record CreateDeletionRequestRequest(string? Note);
    public sealed record ReviewDeletionRequestRequest(
        AccountDeletionRequestStatus Status,
        string? ProcessingNote,
        bool PermanentlyDeleteNow = false);

    [HttpGet("data-summary")]
    public async Task<ActionResult<AccountDataSummaryDto>> GetDataSummary(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyDataSummaryQuery(), ct);
        return Ok(result);
    }

    [HttpGet("deletion-request")]
    public async Task<ActionResult<AccountDeletionRequestDto?>> GetDeletionRequest(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyDeletionRequestQuery(), ct);

        if (result is null)
            return NoContent();

        return Ok(result);
    }

    [HttpPost("deletion-request")]
    public async Task<ActionResult<AccountDeletionRequestDto>> CreateDeletionRequest(
        [FromBody] CreateDeletionRequestRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateMyDeletionRequestCommand(request.Note),
            ct);

        return Ok(result);
    }

    [HttpGet("deletion-requests")]
    public async Task<ActionResult<IReadOnlyList<AccountDeletionRequestDto>>> GetDeletionRequests(
        [FromQuery] int limit = 50,
        [FromQuery] AccountDeletionRequestStatus? status = null,
        CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        var result = await _mediator.Send(
            new ListDeletionRequestsQuery(safeLimit, status),
            ct);

        return Ok(result);
    }

    [HttpGet("deletion-requests/{requestId:guid}")]
    public async Task<ActionResult<AccountDeletionRequestDto>> GetDeletionRequestDetails(
        [FromRoute] Guid requestId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetDeletionRequestDetailsQuery(requestId),
            ct);

        return Ok(result);
    }

    [HttpPatch("deletion-requests/{requestId:guid}")]
    public async Task<ActionResult<AccountDeletionRequestDto>> ReviewDeletionRequest(
        [FromRoute] Guid requestId,
        [FromBody] ReviewDeletionRequestRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ReviewDeletionRequestCommand(
                requestId,
                request.Status,
                request.ProcessingNote,
                request.PermanentlyDeleteNow),
            ct);

        return Ok(result);
    }

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate(CancellationToken ct)
    {
        await _mediator.Send(new DeactivateMyAccountCommand(), ct);
        return NoContent();
    }

    [HttpDelete("permanent")]
    public async Task<IActionResult> PermanentDelete(
        [FromBody] PermanentDeleteAccountRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(
            new PermanentlyDeleteMyAccountCommand(request.Confirmation),
            ct);

        return NoContent();
    }
}