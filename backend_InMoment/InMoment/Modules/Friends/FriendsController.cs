using InMoment.Application.Features.Friends.AcceptRequest;
using InMoment.Application.Features.Friends.Common;
using InMoment.Application.Features.Friends.List;
using InMoment.Application.Features.Friends.ListIncoming;
using InMoment.Application.Features.Friends.ListOutgoing;
using InMoment.Application.Features.Friends.RejectRequest;
using InMoment.Application.Features.Friends.RemoveFriend;
using InMoment.Application.Features.Friends.SendRequest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Friends;

[ApiController]
[Authorize]
[Route("api/friends")]
public sealed class FriendsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FriendsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendDto>>> GetFriends(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListFriendsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestDto>>> GetIncoming(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListIncomingFriendRequestsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("requests/outgoing")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestDto>>> GetOutgoing(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListOutgoingFriendRequestsQuery(), ct);
        return Ok(result);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<object>> SendRequest(
        [FromBody] SendFriendRequestRequest request,
        CancellationToken ct)
    {
        var requestId = await _mediator.Send(
            new SendFriendRequestCommand(request.ToUserId), ct);

        return Ok(new { requestId });
    }

    [HttpPost("requests/{requestId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid requestId, CancellationToken ct)
    {
        await _mediator.Send(new AcceptFriendRequestCommand(requestId), ct);
        return NoContent();
    }

    [HttpPost("requests/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, CancellationToken ct)
    {
        await _mediator.Send(new RejectFriendRequestCommand(requestId), ct);
        return NoContent();
    }

    [HttpDelete("{friendUserId:guid}")]
    public async Task<IActionResult> Remove(Guid friendUserId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveFriendCommand(friendUserId), ct);
        return NoContent();
    }
}