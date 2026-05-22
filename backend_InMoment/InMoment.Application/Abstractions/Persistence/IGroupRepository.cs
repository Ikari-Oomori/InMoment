using DomainGroup = InMoment.Domain.Groups.Group;


namespace InMoment.Application.Abstractions.Persistence;

public interface IGroupRepository
{
    Task AddAsync(DomainGroup group, CancellationToken ct);
    Task<IReadOnlyList<DomainGroup>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<DomainGroup?> GetByIdAsync(Guid groupId, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task AddMemberAsync(InMoment.Domain.Groups.GroupMember member, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetActiveMemberUserIdsAsync(Guid groupId, CancellationToken ct);
    Task<IReadOnlyList<Domain.Groups.Group>> SearchMyGroupsAsync(
    Guid userId,
    string query,
    int limit,
    CancellationToken ct);
}