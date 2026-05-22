using InMoment.Application.Features.Contacts.Common;
using InMoment.Application.Features.Contacts.Import;
using InMoment.Application.Features.Contacts.Invites.Cancel;
using InMoment.Application.Features.Contacts.Invites.Common;
using InMoment.Application.Features.Contacts.Invites.List;
using InMoment.Application.Features.Contacts.Invites.Send;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Contacts;

[ApiController]
[Authorize]
[Route("api/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContactsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportContactsResultDto>> Import(
        [FromBody] ImportContactsRequest request,
        CancellationToken ct)
    {
        var contacts = request.Contacts?
            .Select(x => new ContactImportItemDto(
                x.DisplayName,
                x.Phones ?? Array.Empty<string>(),
                x.Emails ?? Array.Empty<string>()))
            .ToList()
            ?? new List<ContactImportItemDto>();

        var result = await _mediator.Send(new ImportContactsCommand(contacts), ct);
        return Ok(result);
    }

    public sealed record SendContactInviteRequest(
        string? Email,
        string? PhoneNumber,
        string? DisplayName);

    [HttpPost("invites")]
    public async Task<ActionResult<ContactInviteDto>> SendInvite(
        [FromBody] SendContactInviteRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SendContactInviteCommand(
                request.Email,
                request.PhoneNumber,
                request.DisplayName),
            ct);

        return Ok(result);
    }

    [HttpGet("invites")]
    public async Task<ActionResult<IReadOnlyList<ContactInviteDto>>> ListInvites(CancellationToken ct)
        => Ok(await _mediator.Send(new ListMyContactInvitesQuery(), ct));

    [HttpPost("invites/{inviteId:guid}/cancel")]
    public async Task<IActionResult> CancelInvite(Guid inviteId, CancellationToken ct)
    {
        await _mediator.Send(new CancelContactInviteCommand(inviteId), ct);
        return NoContent();
    }
}