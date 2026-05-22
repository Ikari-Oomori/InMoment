using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class InvitationRepository : IInvitationRepository
{
    private readonly AppDbContext _db;

    public InvitationRepository(AppDbContext db) => _db = db;

    public Task AddAsync(GroupInvitation invitation, CancellationToken ct)
        => _db.GroupInvitations.AddAsync(invitation, ct).AsTask();

    public Task<GroupInvitation?> GetByIdAsync(Guid invitationId, CancellationToken ct)
        => _db.GroupInvitations.FirstOrDefaultAsync(x => x.Id == invitationId, ct);

    public Task<bool> HasPendingAsync(Guid groupId, Guid invitedUserId, CancellationToken ct)
        => _db.GroupInvitations.AnyAsync(x =>
            x.GroupId == groupId &&
            x.InvitedUserId == invitedUserId &&
            x.Status == InvitationStatus.Pending, ct);

    public async Task<IReadOnlyList<GroupInvitation>> GetPendingByInvitedUserIdAsync(Guid invitedUserId, CancellationToken ct)
        => await _db.GroupInvitations
            .Where(x => x.InvitedUserId == invitedUserId && x.Status == InvitationStatus.Pending)
            .ToListAsync(ct);

    public Task<bool> InviterIsActiveMemberAsync(Guid groupId, Guid invitedByUserId, CancellationToken ct)
        => _db.GroupMembers.AnyAsync(m =>
            m.GroupId == groupId &&
            m.UserId == invitedByUserId &&
            m.IsActive, ct);

    public async Task<int> CancelPendingByInviterAsync(Guid groupId, Guid invitedByUserId, CancellationToken ct)
    {
        var items = await _db.GroupInvitations
            .Where(x =>
                x.GroupId == groupId &&
                x.InvitedByUserId == invitedByUserId &&
                x.Status == InvitationStatus.Pending)
            .ToListAsync(ct);

        foreach (var item in items)
            item.Cancel();

        return items.Count;
    }

    public async Task<int> CancelPendingByGroupAsync(Guid groupId, CancellationToken ct)
    {
        var items = await _db.GroupInvitations
            .Where(x =>
                x.GroupId == groupId &&
                x.Status == InvitationStatus.Pending)
            .ToListAsync(ct);

        foreach (var item in items)
            item.Cancel();

        return items.Count;
    }
}