using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using InMoment.Application.Abstractions.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Security;

public sealed class IpWhoIsGeoIpResolver : IGeoIpResolver
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<SessionGeoOptions> _options;
    private readonly ILogger<IpWhoIsGeoIpResolver> _logger;

    public IpWhoIsGeoIpResolver(
        HttpClient httpClient,
        IOptions<SessionGeoOptions> options,
        ILogger<IpWhoIsGeoIpResolver> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<GeoIpLocationResult?> ResolveAsync(string? ipAddress, CancellationToken ct)
    {
        if (!_options.Value.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;

        var normalizedIp = ipAddress.Trim();

        if (IsLocalOrPrivate(normalizedIp))
            return null;

        try
        {
            var baseUrl = (_options.Value.BaseUrl ?? "https://ipwho.is").TrimEnd('/');
            var url = $"{baseUrl}/{Uri.EscapeDataString(normalizedIp)}";

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<IpWhoIsResponse>(
                stream,
                cancellationToken: ct);

            if (payload is null || payload.Success == false)
                return null;

            return new GeoIpLocationResult(
                Country: Normalize(payload.Country),
                Region: Normalize(payload.Region),
                City: Normalize(payload.City),
                Provider: "ipwho.is");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geo IP resolve failed for {IpAddress}", normalizedIp);
            return null;
        }
    }

    private static bool IsLocalOrPrivate(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return true;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal)
                return true;

            return false;
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
            return true;

        return bytes[0] switch
        {
            10 => true,
            127 => true,
            169 when bytes[1] == 254 => true,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            192 when bytes[1] == 168 => true,
            _ => false
        };
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..120];
    }

    private sealed class IpWhoIsResponse
    {
        public bool Success { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
    }
}