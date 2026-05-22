using InMoment.Application.Features.Groups.CreateGroup;
using InMoment.Application.Features.Groups.DeleteGroup;
using InMoment.Application.Features.Groups.GetMembers;
using InMoment.Application.Features.Groups.InviteUser;
using InMoment.Application.Features.Groups.LeaveGroup;
using InMoment.Application.Features.Groups.MakeAdmin;
using InMoment.Application.Features.Groups.MyGroups;
using InMoment.Application.Features.Groups.RemoveAdmin;
using InMoment.Application.Features.Groups.RemoveMember;
using InMoment.Application.Features.Groups.TransferOwnership;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Groups;

[ApiController]
[Route("api/groups")]
[Authorize]
public sealed class GroupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GroupsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<CreateGroupResult>> Create(
        [FromBody] CreateGroupRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateGroupCommand(request.Name), ct);
        return Ok(result);
    }

    [HttpPost("{groupId:guid}/invite")]
    public async Task<ActionResult<InviteUserResult>> Invite(
        Guid groupId,
        [FromBody] InviteRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new InviteUserCommand(groupId, request.Login), ct);
        return Ok(result);
    }

    public sealed record InviteRequest(string Login);

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<MyGroupDto>>> My(CancellationToken ct)
    {
        var result = await _mediator.Send(new MyGroupsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{groupId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<GroupMemberDto>>> Members(
        Guid groupId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGroupMembersQuery(groupId), ct);
        return Ok(result);
    }

    [HttpPost("{groupId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid groupId, CancellationToken ct)
    {
        await _mediator.Send(new LeaveGroupCommand(groupId), ct);
        return NoContent();
    }

    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> Delete(Guid groupId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteGroupCommand(groupId), ct);
        return NoContent();
    }

    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveMemberCommand(groupId, userId), ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/members/{userId:guid}/make-admin")]
    public async Task<IActionResult> MakeAdmin(Guid groupId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new MakeAdminCommand(groupId, userId), ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/members/{userId:guid}/remove-admin")]
    public async Task<IActionResult> RemoveAdmin(Guid groupId, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveAdminCommand(groupId, userId), ct);
        return NoContent();
    }

    [HttpPost("{groupId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(
        Guid groupId,
        [FromBody] TransferOwnershipRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new TransferOwnershipCommand(groupId, request.NewOwnerUserId), ct);
        return NoContent();
    }

    public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);
}

public sealed record CreateGroupRequest(string Name);