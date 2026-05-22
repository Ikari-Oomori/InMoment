using InMoment.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace InMoment.API.Realtime;

public sealed class SignalRGroupRealtime : IGroupRealtime
{
    private readonly IHubContext<GroupsHub, IGroupsClient> _hub;

    public SignalRGroupRealtime(IHubContext<GroupsHub, IGroupsClient> hub)
        => _hub = hub;

    public Task NotifyFeedChangedAsync(Guid groupId, string reason, Guid? photoId, CancellationToken ct)
    {
        return _hub.Clients.Group(GroupsHub.GroupName(groupId))
            .FeedChanged(groupId, reason, photoId);
    }
}