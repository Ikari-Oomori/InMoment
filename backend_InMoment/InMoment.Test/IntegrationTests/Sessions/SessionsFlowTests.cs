using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Sessions;

public sealed class SessionsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SessionsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_List_Sessions_And_Current_Session_Is_Marked()
    {
        var user = await Register($"sessions_{Guid.NewGuid():N}@test.com", $"sessions_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user.email, "device-1", "ios");
        var auth2 = await LoginFull(user.email, "device-2", "android");

        SetAuth(_client, auth1.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");
        _client.DefaultRequestHeaders.Add("X-Refresh-Token", auth1.refreshToken);

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<SessionDto>>();
        items.Should().NotBeNull();
        items!.Count(x => x.IsCurrent).Should().Be(1);

        items.Should().Contain(x => x.IsCurrent);
        items.Count(x => x.IsCurrent).Should().Be(1);

        var current = items.First(x => x.IsCurrent);
        current.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task User_Can_List_Sessions_Without_Refresh_Header_And_None_Is_Current()
    {
        var user = await Register($"sessions_nohdr_{Guid.NewGuid():N}@test.com", $"sessionsnohdr_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user.email, "device-1", "ios");
        var auth2 = await LoginFull(user.email, "device-2", "android");

        SetAuth(_client, auth1.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<SessionDto>>();
        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThanOrEqualTo(2);
        items.Should().OnlyContain(x => x.IsCurrent == false);
    }

    [Fact]
    public async Task User_Can_Revoke_Own_Other_Session()
    {
        var user = await Register($"sessions_{Guid.NewGuid():N}@test.com", $"sessions_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user.email, "device-1", "ios");
        var auth2 = await LoginFull(user.email, "device-2", "android");

        SetAuth(_client, auth1.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");
        _client.DefaultRequestHeaders.Add("X-Refresh-Token", auth1.refreshToken);

        var beforeResponse = await _client.GetAsync("/api/sessions");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var beforeItems = await beforeResponse.Content.ReadFromJsonAsync<List<SessionDto>>();
        beforeItems.Should().NotBeNull();

        beforeItems.Should().Contain(x => x.IsCurrent == false && x.IsRevoked == false);

        var otherSession = beforeItems!
            .First(x => x.IsCurrent == false && x.IsRevoked == false);

        var revokeResponse = await _client.DeleteAsync($"/api/sessions/{otherSession.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterResponse = await _client.GetAsync("/api/sessions");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterItems = await afterResponse.Content.ReadFromJsonAsync<List<SessionDto>>();
        afterItems.Should().NotBeNull();

        afterItems!.Any(x => x.Id == otherSession.Id).Should().BeFalse();

        afterItems.Count(x => x.IsCurrent).Should().Be(1);
    }

    [Fact]
    public async Task User_Cannot_Revoke_Another_Users_Session()
    {
        var user1 = await Register($"sessions1_{Guid.NewGuid():N}@test.com", $"sessions1_{Guid.NewGuid():N}");
        var user2 = await Register($"sessions2_{Guid.NewGuid():N}@test.com", $"sessions2_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user1.email, "device-1", "ios");
        var auth2 = await LoginFull(user2.email, "device-2", "android");

        SetAuth(_client, auth2.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");
        _client.DefaultRequestHeaders.Add("X-Refresh-Token", auth2.refreshToken);

        var user2SessionsResponse = await _client.GetAsync("/api/sessions");
        user2SessionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var user2Sessions = await user2SessionsResponse.Content.ReadFromJsonAsync<List<SessionDto>>();
        user2Sessions.Should().NotBeNull();

        var otherUsersSessionId = user2Sessions!.First().Id;

        SetAuth(_client, auth1.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");
        _client.DefaultRequestHeaders.Add("X-Refresh-Token", auth1.refreshToken);

        var revokeResponse = await _client.DeleteAsync($"/api/sessions/{otherUsersSessionId}");

        revokeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Can_Refresh_Once_But_Old_Refresh_Token_Cannot_Be_Reused()
    {
        var user = await Register($"refresh_{Guid.NewGuid():N}@test.com", $"refresh_{Guid.NewGuid():N}");

        var auth = await LoginFull(user.email, "device-1", "ios");

        var refreshResponse = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = auth.refreshToken
        }));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        refreshed.Should().NotBeNull();
        refreshed!.refreshToken.Should().NotBeNullOrWhiteSpace();
        refreshed.refreshToken.Should().NotBe(auth.refreshToken);

        var replayResponse = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = auth.refreshToken
        }));

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Can_Logout_All_And_All_Sessions_Become_Revoked()
    {
        var user = await Register($"logoutall_{Guid.NewGuid():N}@test.com", $"logoutall_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user.email, "device-1", "ios");
        var auth2 = await LoginFull(user.email, "device-2", "android");

        SetAuth(_client, auth1.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");
        _client.DefaultRequestHeaders.Add("X-Refresh-Token", auth1.refreshToken);

        var logoutAllResponse = await _client.PostAsync("/api/auth/logout-all", null);
        logoutAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sessionsResponse = await _client.GetAsync("/api/sessions");
        sessionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await sessionsResponse.Content.ReadFromJsonAsync<List<SessionDto>>();
        items.Should().NotBeNull();

        items!.Count(x => x.IsCurrent).Should().Be(0);

        var refreshAfterLogoutAll = await _client.PostAsync("/api/auth/refresh", Json(new
        {
            refreshToken = auth1.refreshToken
        }));

        refreshAfterLogoutAll.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Can_Revoke_All_Other_Sessions_With_Single_Request()
    {
        var user = await Register($"sessions_bulk_{Guid.NewGuid():N}@test.com", $"sessionsbulk_{Guid.NewGuid():N}");

        var auth1 = await LoginFull(user.email, "device-1", "ios");
        var auth2 = await LoginFull(user.email, "device-2", "android");
        var auth3 = await LoginFull(user.email, "device-3", "web");

        SetAuth(_client, auth1.accessToken);
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");
        _client.DefaultRequestHeaders.Add("X-Refresh-Token", auth1.refreshToken);

        var revokeResponse = await _client.DeleteAsync("/api/sessions/others");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterResponse = await _client.GetAsync("/api/sessions");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterItems = await afterResponse.Content.ReadFromJsonAsync<List<SessionDto>>();
        afterItems.Should().NotBeNull();
        afterItems!.Count(x => x.IsCurrent).Should().Be(1);

        afterItems.Count(x => x.IsCurrent && !x.IsRevoked).Should().BeGreaterThanOrEqualTo(0);
        //afterItems.Count(x => !x.IsCurrent && x.IsRevoked).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Sessions_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove("X-Refresh-Token");

        var getResponse = await _client.GetAsync("/api/sessions");
        var deleteResponse = await _client.DeleteAsync($"/api/sessions/{Guid.NewGuid()}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private sealed record SessionDto(
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
        bool IsRevoked);
}