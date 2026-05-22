using InMoment.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InMoment.API.Realtime;

public interface IGroupsClient
{
    Task FeedChanged(Guid groupId, string reason, Guid? photoId);
}

[Authorize]
public sealed class GroupsHub : Hub<IGroupsClient>
{
    private readonly IGroupRepository _groups;
    private readonly ILogger<GroupsHub> _logger;

    public GroupsHub(
        IGroupRepository groups,
        ILogger<GroupsHub> logger)
    {
        _groups = groups;
        _logger = logger;
    }

    public async Task JoinGroup(Guid groupId)
    {
        if (groupId == Guid.Empty)
            throw new HubException("GroupId is required.");

        var userId = GetUserId();

        var isMember = await _groups.IsMemberAsync(groupId, userId, Context.ConnectionAborted);
        if (!isMember)
        {
            _logger.LogWarning(
                "SignalR join denied. User {UserId} tried to join group {GroupId}. ConnectionId: {ConnectionId}",
                userId,
                groupId,
                Context.ConnectionId);

            throw new HubException("You are not an active member of this group.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(groupId),
            Context.ConnectionAborted);
    }

    public async Task LeaveGroup(Guid groupId)
    {
        if (groupId == Guid.Empty)
            throw new HubException("GroupId is required.");

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GroupName(groupId),
            Context.ConnectionAborted);
    }

    internal static string GroupName(Guid groupId) => $"group:{groupId:D}";

    private Guid GetUserId()
    {
        var raw = Context.User?.FindFirst("sub")?.Value
                  ?? Context.User?.FindFirst("nameid")?.Value
                  ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var userId)
            ? userId
            : throw new HubException("Unauthorized");
    }
}