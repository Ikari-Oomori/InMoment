namespace InMoment.Application.Abstractions.Security;

public interface IGeoIpResolver
{
    Task<GeoIpLocationResult?> ResolveAsync(string? ipAddress, CancellationToken ct);
}