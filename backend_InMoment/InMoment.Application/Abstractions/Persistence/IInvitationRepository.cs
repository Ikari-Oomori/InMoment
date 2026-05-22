using InMoment.Domain.Groups;

namespace InMoment.Application.Abstractions.Persistence;

public interface IInvitationRepository
{
    Task AddAsync(GroupInvitation invitation, CancellationToken ct);

    Task<GroupInvitation?> GetByIdAsync(Guid invitationId, CancellationToken ct);

    Task<bool> HasPendingAsync(Guid groupId, Guid invitedUserId, CancellationToken ct);

    Task<IReadOnlyList<GroupInvitation>> GetPendingByInvitedUserIdAsync(Guid invitedUserId, CancellationToken ct);

    Task<bool> InviterIsActiveMemberAsync(Guid groupId, Guid invitedByUserId, CancellationToken ct);
    Task<int> CancelPendingByInviterAsync(Guid groupId, Guid invitedByUserId, CancellationToken ct);
    Task<int> CancelPendingByGroupAsync(Guid groupId, CancellationToken ct);
    
}