using InMoment.Domain.Security;

namespace InMoment.Application.Abstractions.Persistence;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken ct);
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<PasswordResetToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);
}