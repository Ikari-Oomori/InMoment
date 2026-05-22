using System.Security.Claims;
using FluentAssertions;
using InMoment.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace InMoment.Infrastructure.Tests.Auth;

public sealed class CurrentUserTests
{
    [Fact]
    public void Constructor_ShouldReturnEmptyGuid_WhenHttpContextMissing()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = null
        };

        var currentUser = new CurrentUser(accessor);

        currentUser.UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Constructor_ShouldReturnEmptyGuid_WhenClaimMissing()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var currentUser = new CurrentUser(accessor);

        currentUser.UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Constructor_ShouldReturnEmptyGuid_WhenClaimIsInvalidGuid()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
        });

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var currentUser = new CurrentUser(accessor);

        currentUser.UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Constructor_ShouldParseUserId_WhenValidNameIdentifierExists()
    {
        var userId = Guid.NewGuid();

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        });

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var currentUser = new CurrentUser(accessor);

        currentUser.UserId.Should().Be(userId);
    }
}