namespace InMoment.Application.Features.Sessions.List;

public sealed record SessionDto(
    Guid Id,
    string? DeviceName,
    string? Platform,
    string? IpAddress,
    string? UserAgent,
    string? GeoCountry,
    string? GeoRegion,
    string? GeoCity,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCurrent,
    bool IsRevoked
);