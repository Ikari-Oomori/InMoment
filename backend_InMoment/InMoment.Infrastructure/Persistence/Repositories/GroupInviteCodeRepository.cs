using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class GroupInviteCodeRepository : IGroupInviteCodeRepository
{
    private readonly AppDbContext _db;

    public GroupInviteCodeRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(GroupInviteCode code, CancellationToken ct)
        => _db.GroupInviteCodes.AddAsync(code, ct).AsTask();

    public Task<GroupInviteCode?> GetByCodeAsync(string code, CancellationToken ct)
        => _db.GroupInviteCodes
            .FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<IReadOnlyList<GroupInviteCode>> GetByGroupIdAsync(Guid groupId, CancellationToken ct)
    {
        return await _db.GroupInviteCodes
            .Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }
}