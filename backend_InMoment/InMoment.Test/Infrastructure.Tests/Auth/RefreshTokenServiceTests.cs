using FluentAssertions;
using InMoment.Infrastructure.Auth;

namespace InMoment.Infrastructure.Tests.Auth;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public void CreateToken_ShouldReturnUrlSafeNonEmptyToken()
    {
        var service = new RefreshTokenService();

        var token = service.CreateToken();

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void CreateToken_ShouldReturnDifferentTokens()
    {
        var service = new RefreshTokenService();

        var first = service.CreateToken();
        var second = service.CreateToken();

        first.Should().NotBe(second);
    }

    [Fact]
    public void HashToken_ShouldReturnDeterministicUppercaseHexHash()
    {
        var service = new RefreshTokenService();

        var first = service.HashToken("raw-token");
        var second = service.HashToken("raw-token");

        first.Should().Be(second);
        first.Should().HaveLength(64);
        first.Should().MatchRegex("^[0-9A-F]+$");
    }

    [Fact]
    public void GetExpiryUtc_ShouldReturnAboutThirtyDaysFromNow()
    {
        var service = new RefreshTokenService();

        var before = DateTime.UtcNow;
        var expiry = service.GetExpiryUtc();
        var after = DateTime.UtcNow;

        expiry.Should().BeAfter(before.AddDays(29));
        expiry.Should().BeBefore(after.AddDays(31));
    }
}