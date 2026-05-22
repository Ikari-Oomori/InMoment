using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Auth;

public sealed class AuthFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Login_Refresh_And_Logout_Flow_Works()
    {
        var email = $"auth_{Guid.NewGuid():N}@test.com";
        var userName = $"auth_{Guid.NewGuid():N}";

        var registerResponse = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName = "Auth",
            lastName = "User",
            userName
        }));

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registerAuth.Should().NotBeNull();
        registerAuth!.userId.Should().NotBeEmpty();
        registerAuth.accessToken.Should().NotBeNullOrWhiteSpace();
        registerAuth.refreshToken.Should().NotBeNullOrWhiteSpace();

        var loginResponse = await _client.PostAsync("/api/auth/login", Json(new
        {
            email,
            password = "Pass123!",
            deviceName = "iphone",
            platform = "ios"
        }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginAuth.Should().NotBeNull();
        loginAuth!.userId.Should().Be(registerAuth.userId);
        loginAuth.accessToken.Should().NotBeNullOrWhiteSpace();
        loginAuth.refreshToken.Should().NotBeNullOrWhiteSpace();

        var refreshResponse = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = loginAuth.refreshToken
        }));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponse>();
        refreshed.Should().NotBeNull();
        refreshed!.accessToken.Should().NotBeNullOrWhiteSpace();
        refreshed.refreshToken.Should().NotBeNullOrWhiteSpace();

        SetAuth(_client, refreshed.accessToken);

        var logoutResponse = await _client.PostAsync("/api/auth/logout", Json(new
        {
            refreshToken = refreshed.refreshToken
        }));

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAfterLogoutResponse = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = refreshed.refreshToken
        }));

        refreshAfterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LogoutAll_Revokes_All_RefreshTokens()
    {
        var user = await Register($"logoutall_{Guid.NewGuid():N}@test.com", $"logoutall_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user.email, "device-1", "ios");
        var auth2 = await LoginFull(user.email, "device-2", "android");

        SetAuth(_client, auth1.accessToken);

        var logoutAllResponse = await _client.PostAsync("/api/auth/logout-all", null);
        logoutAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refresh1Response = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = auth1.refreshToken
        }));

        refresh1Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var refresh2Response = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = auth2.refreshToken
        }));

        refresh2Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_With_Invalid_Password_Returns_Forbidden()
    {
        var user = await Register($"badlogin_{Guid.NewGuid():N}@test.com", $"badlogin_{Guid.NewGuid():N}");

        var response = await _client.PostAsync("/api/auth/login", Json(new
        {
            email = user.email,
            password = "Wrong123!",
            deviceName = "test",
            platform = "test"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refresh_With_Invalid_Token_Returns_Forbidden()
    {
        var response = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = "definitely-invalid-token"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Protected_Auth_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var logoutResponse = await _client.PostAsync("/api/auth/logout", Json(new
        {
            refreshToken = "whatever"
        }));

        var logoutAllResponse = await _client.PostAsync("/api/auth/logout-all", null);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        logoutAllResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string email, Guid id)> Register(string email, string userName)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName = "Test",
            lastName = "User",
            userName
        }));

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return (email, auth!.userId);
    }

    private async Task<AuthResponse> LoginFull(string email, string deviceName, string platform)
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new
        {
            email,
            password = "Pass123!",
            deviceName,
            platform
        }));

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return auth!;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record RefreshResponse(
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);
}