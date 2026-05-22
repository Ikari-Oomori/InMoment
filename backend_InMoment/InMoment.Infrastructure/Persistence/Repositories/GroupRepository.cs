using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _db;

    public GroupRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Group group, CancellationToken ct)
    {
        await _db.Groups.AddAsync(group, ct);
    }

    public Task<Group?> GetByIdAsync(Guid groupId, CancellationToken ct)
    {
        return _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.IsActive, ct);
    }

    public async Task<IReadOnlyList<Group>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var groupIds = await _db.GroupMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        return await _db.Groups
            .Include(g => g.Members)
            .Where(g => groupIds.Contains(g.Id) && g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberUserIdsAsync(Guid groupId, CancellationToken ct)
    {
        return await _db.Set<GroupMember>()
            .AsNoTracking()
            .Where(x => x.GroupId == groupId && x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.IsActive, ct);

    public Task AddMemberAsync(GroupMember member, CancellationToken ct)
        => _db.GroupMembers.AddAsync(member, ct).AsTask();

    public async Task<IReadOnlyList<Domain.Groups.Group>> SearchMyGroupsAsync(
        Guid userId,
        string query,
        int limit,
        CancellationToken ct)
    {
        query = query.Trim().ToLower();

        return await _db.Groups
            .AsNoTracking()
            .Include(g => g.Members)
            .Where(g =>
                g.Name.ToLower().Contains(query) &&
                g.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderBy(g => g.Name)
            .Take(limit)
            .ToListAsync(ct);
    }
}