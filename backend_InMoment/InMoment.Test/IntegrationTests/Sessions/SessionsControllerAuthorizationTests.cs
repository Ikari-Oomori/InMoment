using System.Linq;
using FluentAssertions;
using InMoment.API.Modules.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.Tests.IntegrationTests.Sessions;

public sealed class SessionsControllerAuthorizationTests
{
    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        typeof(SessionsController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldRequireAuthorization()
    {
        typeof(SessionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Controller_ShouldHaveExpectedRoute()
    {
        var route = typeof(SessionsController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();

        route.Template.Should().Be("api/sessions");
    }

    [Fact]
    public void Get_Action_ShouldBeHttpGet()
    {
        var method = typeof(SessionsController).GetMethod(nameof(SessionsController.Get));
        method.Should().NotBeNull();

        method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: true)
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public void Revoke_Action_ShouldBeHttpDelete_WithGuidRoute()
    {
        var method = typeof(SessionsController).GetMethod(nameof(SessionsController.Revoke));
        method.Should().NotBeNull();

        var attr = method!
            .GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true)
            .Cast<HttpDeleteAttribute>()
            .Single();

        attr.Template.Should().Be("{id:guid}");
    }
}