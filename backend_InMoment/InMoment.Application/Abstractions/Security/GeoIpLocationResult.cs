namespace InMoment.Application.Abstractions.Security;

public sealed record GeoIpLocationResult(
    string? Country,
    string? Region,
    string? City,
    string? Provider
);