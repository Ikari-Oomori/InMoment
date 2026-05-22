using InMoment.Domain.Common;

namespace InMoment.Domain.Notifications;

public sealed class DeviceToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public PushPlatform Platform { get; private set; }
    public PushProvider Provider { get; private set; }
    public string? DeviceName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime LastUsedAtUtc { get; private set; }

    private DeviceToken() { }

    public static DeviceToken Register(
        Guid userId,
        string token,
        PushPlatform platform,
        PushProvider provider,
        string? deviceName)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var normalizedToken = NormalizeToken(token);
        var normalizedDeviceName = NormalizeDeviceName(deviceName);
        var now = DateTime.UtcNow;

        return new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = normalizedToken,
            Platform = platform,
            Provider = provider,
            DeviceName = normalizedDeviceName,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastUsedAtUtc = now
        };
    }

    public void Refresh(
        PushPlatform platform,
        PushProvider provider,
        string? deviceName)
    {
        Platform = platform;
        Provider = provider;
        DeviceName = NormalizeDeviceName(deviceName);
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
        LastUsedAtUtc = DateTime.UtcNow;
    }

    public void ReassignToUser(
        Guid userId,
        PushPlatform platform,
        PushProvider provider,
        string? deviceName)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        UserId = userId;
        Refresh(platform, provider, deviceName);
    }

    public void MarkUsed()
    {
        LastUsedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("Token is required.");

        var normalized = token.Trim();

        if (normalized.Length > 4000)
            throw new ValidationException("Token is too long.");

        return normalized;
    }

    private static string? NormalizeDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return null;

        var normalized = deviceName.Trim();

        if (normalized.Length > 200)
            throw new ValidationException("DeviceName is too long.");

        return normalized;
    }
}