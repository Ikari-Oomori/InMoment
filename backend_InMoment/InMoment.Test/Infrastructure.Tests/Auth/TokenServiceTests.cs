using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using InMoment.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Tests.Auth;

public sealed class TokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ShouldBuildJwt_WithExpectedClaimsIssuerAudienceAndExpiry()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "InMoment",
            Audience = "InMoment.Mobile",
            SigningKey = "super_secret_signing_key_1234567890",
            AccessTokenMinutes = 15
        });

        var service = new TokenService(options);

        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var token = service.CreateAccessToken(userId, "anna_user");

        token.Should().NotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("InMoment");
        jwt.Audiences.Should().ContainSingle().Which.Should().Be("InMoment.Mobile");

        jwt.Claims.Should().Contain(x =>
            x.Type == ClaimTypes.NameIdentifier &&
            x.Value == userId.ToString());

        jwt.Claims.Should().Contain(x =>
            x.Type == ClaimTypes.Name &&
            x.Value == "anna_user");

        jwt.ValidTo.Should().BeAfter(before.AddMinutes(14));
        jwt.ValidTo.Should().BeBefore(DateTime.UtcNow.AddMinutes(16));
    }
}