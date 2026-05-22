using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InMoment.API.Realtime;

public interface IUsersClient
{
    Task NotificationsChanged(int unreadCount);
}

[Authorize]
public sealed class UsersHub : Hub<IUsersClient>
{
    public Task JoinSelf()
        => Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(GetUserId()));

    public Task LeaveSelf()
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroupName(GetUserId()));

    internal static string UserGroupName(Guid userId) => $"user:{userId:D}";

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