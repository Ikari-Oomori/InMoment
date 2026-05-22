using InMoment.Domain.Common;

namespace InMoment.Domain.Security;

public sealed class RefreshSession : Entity<Guid>
{
    private RefreshSession() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string? DeviceName { get; private set; }
    public string? Platform { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public string? GeoCountry { get; private set; }
    public string? GeoRegion { get; private set; }
    public string? GeoCity { get; private set; }
    public string? GeoProvider { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokeReason { get; private set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public static RefreshSession Create(
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? deviceName,
        string? platform,
        string? ipAddress,
        string? userAgent,
        string? geoCountry,
        string? geoRegion,
        string? geoCity,
        string? geoProvider)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ValidationException("Token hash is required.");

        if (expiresAtUtc <= createdAtUtc)
            throw new ValidationException("Refresh token expiry is invalid.");

        return new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            DeviceName = Normalize(deviceName, 200),
            Platform = Normalize(platform, 100),
            IpAddress = Normalize(ipAddress, 100),
            UserAgent = Normalize(userAgent, 1000),
            GeoCountry = Normalize(geoCountry, 120),
            GeoRegion = Normalize(geoRegion, 120),
            GeoCity = Normalize(geoCity, 120),
            GeoProvider = Normalize(geoProvider, 80)
        };
    }

    public bool IsActive(DateTime nowUtc)
        => !IsRevoked && ExpiresAtUtc > nowUtc;

    public void MarkUsed(DateTime usedAtUtc)
    {
        if (IsRevoked)
            throw new ValidationException("Session is revoked.");

        LastUsedAtUtc = usedAtUtc;
    }

    public void Rotate(string newTokenHash, DateTime newExpiresAtUtc, DateTime nowUtc)
    {
        if (IsRevoked)
            throw new ValidationException("Session is revoked.");

        if (string.IsNullOrWhiteSpace(newTokenHash))
            throw new ValidationException("New token hash is required.");

        if (newExpiresAtUtc <= nowUtc)
            throw new ValidationException("Refresh token expiry is invalid.");

        TokenHash = newTokenHash;
        ExpiresAtUtc = newExpiresAtUtc;
        LastUsedAtUtc = nowUtc;
    }

    public void Revoke(string reason, DateTime nowUtc)
    {
        if (IsRevoked)
            return;

        RevokedAtUtc = nowUtc;
        RevokeReason = Normalize(reason, 300);
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