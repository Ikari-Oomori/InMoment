using InMoment.Domain.SystemAnnouncements;

namespace InMoment.Application.Abstractions.Persistence;

public interface ISystemAnnouncementRepository
{
    Task AddAsync(SystemAnnouncement announcement, CancellationToken ct);

    Task<SystemAnnouncement?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<SystemAnnouncement>> GetLatestAsync(int limit, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, SystemAnnouncement>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct);

    void Remove(SystemAnnouncement announcement);
}