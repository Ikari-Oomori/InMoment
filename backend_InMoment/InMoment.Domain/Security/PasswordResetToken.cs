using InMoment.Domain.Common;

namespace InMoment.Domain.Security;

public sealed class PasswordResetToken : Entity<Guid>
{
    private PasswordResetToken() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RequestedByIp { get; private set; }
    public string? RequestedByUserAgent { get; private set; }

    public bool IsUsed => UsedAtUtc.HasValue;
    public bool IsRevoked => RevokedAtUtc.HasValue;

    public static PasswordResetToken Create(
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? requestedByIp,
        string? requestedByUserAgent)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ValidationException("Token hash is required.");

        if (expiresAtUtc <= createdAtUtc)
            throw new ValidationException("Password reset token expiry is invalid.");

        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            RequestedByIp = Normalize(requestedByIp, 100),
            RequestedByUserAgent = Normalize(requestedByUserAgent, 1000)
        };
    }

    public bool IsActive(DateTime nowUtc)
        => !IsUsed && !IsRevoked && ExpiresAtUtc > nowUtc;

    public void MarkUsed(DateTime nowUtc)
    {
        if (IsUsed)
            throw new ValidationException("Reset token already used.");

        if (IsRevoked)
            throw new ValidationException("Reset token is revoked.");

        UsedAtUtc = nowUtc;
    }

    public void Revoke(DateTime nowUtc)
    {
        if (IsRevoked)
            return;

        RevokedAtUtc = nowUtc;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}