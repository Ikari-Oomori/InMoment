using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Privacy;

public sealed class PrivacyFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PrivacyFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Get_Default_Privacy_And_Update_It()
    {
        var email = $"privacy_{Guid.NewGuid():N}@test.com";
        var userName = $"privacyuser_{Guid.NewGuid():N}";

        await Register(email, userName);
        var token = await Login(email);

        SetAuth(_client, token);

        var getDefaultResponse = await _client.GetAsync("/api/privacy");
        getDefaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var defaultPrivacy = await getDefaultResponse.Content.ReadFromJsonAsync<PrivacySettingsDto>();
        defaultPrivacy.Should().NotBeNull();
        defaultPrivacy!.AllowFriendRequestsFrom.Should().Be(1);
        defaultPrivacy.AllowGroupInvitesFrom.Should().Be(1);
        defaultPrivacy.DiscoverableByContacts.Should().BeTrue();
        defaultPrivacy.DiscoverableBySearch.Should().BeTrue();

        var updateResponse = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 2,
            allowGroupInvitesFrom = 3,
            discoverableByContacts = false,
            discoverableBySearch = false
        }));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getUpdatedResponse = await _client.GetAsync("/api/privacy");
        getUpdatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedPrivacy = await getUpdatedResponse.Content.ReadFromJsonAsync<PrivacySettingsDto>();
        updatedPrivacy.Should().NotBeNull();
        updatedPrivacy!.AllowFriendRequestsFrom.Should().Be(2);
        updatedPrivacy.AllowGroupInvitesFrom.Should().Be(3);
        updatedPrivacy.DiscoverableByContacts.Should().BeFalse();
        updatedPrivacy.DiscoverableBySearch.Should().BeFalse();
    }

    [Fact]
    public async Task Privacy_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var getResponse = await _client.GetAsync("/api/privacy");
        var patchResponse = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 1,
            allowGroupInvitesFrom = 1,
            discoverableByContacts = true,
            discoverableBySearch = true
        }));

        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task Register(string email, string userName)
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
    }

    private async Task<string> Login(string email)
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new
        {
            email,
            password = "Pass123!",
            deviceName = "tests",
            platform = "tests"
        }));

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return auth!.accessToken;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record PrivacySettingsDto(
        int AllowFriendRequestsFrom,
        int AllowGroupInvitesFrom,
        bool DiscoverableByContacts,
        bool DiscoverableBySearch);
}