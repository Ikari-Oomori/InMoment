namespace InMoment.Application.Abstractions.Queries;

public interface INotificationPreviewReader
{
    Task<NotificationPreviewBundle> GetBundleAsync(
        IReadOnlyList<Guid> actorUserIds,
        IReadOnlyList<Guid> groupIds,
        IReadOnlyList<Guid> photoIds,
        CancellationToken ct);
}

public sealed record NotificationActorPreview(
    Guid UserId,
    string DisplayName,
    string UserName,
    string? ProfilePhotoUrl
);

public sealed record NotificationGroupPreview(
    Guid GroupId,
    string Name,
    string? AvatarUrl
);

public sealed record NotificationPhotoPreview(
    Guid PhotoId,
    string StorageKey,
    string? Caption
);

public sealed record NotificationPreviewBundle(
    IReadOnlyDictionary<Guid, NotificationActorPreview> Actors,
    IReadOnlyDictionary<Guid, NotificationGroupPreview> Groups,
    IReadOnlyDictionary<Guid, NotificationPhotoPreview> Photos
);