using InMoment.Domain.Users;

namespace InMoment.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task<bool> UserNameExistsAsync(string userName, CancellationToken ct);
    Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken ct);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<User?> GetByDeletedEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByDeletedUserNameAsync(string userName, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(CancellationToken ct);

    Task<IReadOnlyList<Domain.Users.User>> SearchAsync(
        string query,
        int limit,
        Guid currentUserId,
        CancellationToken ct);

    Task<IReadOnlyList<Domain.Users.User>> SearchByPrefixAsync(
        string prefix,
        int limit,
        Guid currentUserId,
        CancellationToken ct);

    Task<IReadOnlyList<User>> GetByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken ct);
}