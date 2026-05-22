using System.Security.Cryptography;
using System.Text;
using InMoment.Application.Abstractions.Security;

namespace InMoment.Infrastructure.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    public string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public DateTime GetExpiryUtc() => DateTime.UtcNow.Add(Lifetime);
}