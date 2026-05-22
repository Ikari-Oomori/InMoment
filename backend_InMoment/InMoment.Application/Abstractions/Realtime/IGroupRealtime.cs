namespace InMoment.Application.Abstractions.Realtime;

public interface IGroupRealtime
{
    Task NotifyFeedChangedAsync(Guid groupId, string reason, Guid? photoId, CancellationToken ct);
}