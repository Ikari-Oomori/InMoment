using InMoment.Domain.Groups;

namespace InMoment.Application.Abstractions.Persistence;

public interface IGroupInviteCodeRepository
{
    Task AddAsync(GroupInviteCode code, CancellationToken ct);

    Task<GroupInviteCode?> GetByCodeAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<GroupInviteCode>> GetByGroupIdAsync(Guid groupId, CancellationToken ct);
}