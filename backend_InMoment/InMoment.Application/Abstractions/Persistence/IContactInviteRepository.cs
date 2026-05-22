using InMoment.Domain.Contacts;

namespace InMoment.Application.Abstractions.Persistence;

public interface IContactInviteRepository
{
    Task AddAsync(ContactInvite invite, CancellationToken ct);
    Task<ContactInvite?> GetByIdAsync(Guid inviteId, CancellationToken ct);
    Task<IReadOnlyList<ContactInvite>> GetByUserIdAsync(Guid userId, CancellationToken ct);

    Task<ContactInvite?> GetPendingByEmailAsync(Guid userId, string email, CancellationToken ct);
    Task<ContactInvite?> GetPendingByPhoneAsync(Guid userId, string phoneNumber, CancellationToken ct);
}