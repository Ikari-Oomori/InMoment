using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Friends;

public sealed class FriendshipFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FriendshipFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Users_Can_SendAccept_And_Remove_Friendship()
    {
        var userA = await Register($"usera_{Guid.NewGuid():N}@test.com", $"UserA{Guid.NewGuid():N}");
        var userB = await Register($"userb_{Guid.NewGuid():N}@test.com", $"UserB{Guid.NewGuid():N}");

        var userAToken = await Login(userA.email);
        SetAuth(_client, userAToken);

        var sendResponse = await _client.PostAsync(
            "/api/friends/requests",
            Json(new
            {
                toUserId = userB.id
            }));

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sendJson = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        sendJson.TryGetProperty("requestId", out var requestIdElement).Should().BeTrue();
        var requestId = requestIdElement.GetGuid();
        requestId.Should().NotBeEmpty();

        var userBToken = await Login(userB.email);
        SetAuth(_client, userBToken);

        var incomingResponse = await _client.GetAsync("/api/friends/requests/incoming");
        incomingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var incomingJson = await incomingResponse.Content.ReadFromJsonAsync<JsonElement>();
        incomingJson.ValueKind.Should().Be(JsonValueKind.Array);
        incomingJson.GetArrayLength().Should().Be(1);

        var incomingRequestId = incomingJson[0].GetProperty("requestId").GetGuid();
        incomingRequestId.Should().Be(requestId);

        var acceptResponse = await _client.PostAsync(
            $"/api/friends/requests/{requestId}/accept",
            null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        userAToken = await Login(userA.email);
        SetAuth(_client, userAToken);

        var friendsAResponse = await _client.GetAsync("/api/friends");
        friendsAResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var friendsAJson = await friendsAResponse.Content.ReadFromJsonAsync<JsonElement>();
        friendsAJson.ValueKind.Should().Be(JsonValueKind.Array);
        friendsAJson.GetArrayLength().Should().Be(1);
        friendsAJson[0].GetProperty("userId").GetGuid().Should().Be(userB.id);

        userBToken = await Login(userB.email);
        SetAuth(_client, userBToken);

        var friendsBResponse = await _client.GetAsync("/api/friends");
        friendsBResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var friendsBJson = await friendsBResponse.Content.ReadFromJsonAsync<JsonElement>();
        friendsBJson.ValueKind.Should().Be(JsonValueKind.Array);
        friendsBJson.GetArrayLength().Should().Be(1);
        friendsBJson[0].GetProperty("userId").GetGuid().Should().Be(userA.id);

        userAToken = await Login(userA.email);
        SetAuth(_client, userAToken);

        var removeResponse = await _client.DeleteAsync($"/api/friends/{userB.id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var friendsAAfterRemove = await _client.GetAsync("/api/friends");
        friendsAAfterRemove.StatusCode.Should().Be(HttpStatusCode.OK);

        var friendsAAfterRemoveJson = await friendsAAfterRemove.Content.ReadFromJsonAsync<JsonElement>();
        friendsAAfterRemoveJson.ValueKind.Should().Be(JsonValueKind.Array);
        friendsAAfterRemoveJson.GetArrayLength().Should().Be(0);

        userBToken = await Login(userB.email);
        SetAuth(_client, userBToken);

        var friendsBAfterRemove = await _client.GetAsync("/api/friends");
        friendsBAfterRemove.StatusCode.Should().Be(HttpStatusCode.OK);

        var friendsBAfterRemoveJson = await friendsBAfterRemove.Content.ReadFromJsonAsync<JsonElement>();
        friendsBAfterRemoveJson.ValueKind.Should().Be(JsonValueKind.Array);
        friendsBAfterRemoveJson.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task User_Cannot_Send_Friend_Request_To_Self()
    {
        var user = await Register($"self_{Guid.NewGuid():N}@test.com", $"Self{Guid.NewGuid():N}");
        var token = await Login(user.email);
        SetAuth(_client, token);

        var response = await _client.PostAsync(
            "/api/friends/requests",
            Json(new
            {
                toUserId = user.id
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_Cannot_Send_Friend_Request_When_Target_Privacy_Disables_Requests()
    {
        var sender = await Register($"sender_{Guid.NewGuid():N}@test.com", $"Sender{Guid.NewGuid():N}");
        var target = await Register($"target_{Guid.NewGuid():N}@test.com", $"Target{Guid.NewGuid():N}");

        var targetToken = await Login(target.email);
        SetAuth(_client, targetToken);

        var patchPrivacy = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 3,
            allowGroupInvitesFrom = 1,
            discoverableByContacts = true,
            discoverableBySearch = true
        }));

        patchPrivacy.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var senderToken = await Login(sender.email);
        SetAuth(_client, senderToken);

        var response = await _client.PostAsync(
            "/api/friends/requests",
            Json(new
            {
                toUserId = target.id
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Can_Reject_Incoming_Friend_Request()
    {
        var userA = await Register($"ra_{Guid.NewGuid():N}@test.com", $"RejectA{Guid.NewGuid():N}");
        var userB = await Register($"rb_{Guid.NewGuid():N}@test.com", $"RejectB{Guid.NewGuid():N}");

        var userAToken = await Login(userA.email);
        SetAuth(_client, userAToken);

        var sendResponse = await _client.PostAsync(
            "/api/friends/requests",
            Json(new
            {
                toUserId = userB.id
            }));

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sendJson = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = sendJson.GetProperty("requestId").GetGuid();

        var userBToken = await Login(userB.email);
        SetAuth(_client, userBToken);

        var rejectResponse = await _client.PostAsync(
            $"/api/friends/requests/{requestId}/reject",
            null);

        rejectResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var friendsResponse = await _client.GetAsync("/api/friends");
        friendsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var friendsJson = await friendsResponse.Content.ReadFromJsonAsync<JsonElement>();
        friendsJson.ValueKind.Should().Be(JsonValueKind.Array);
        friendsJson.GetArrayLength().Should().Be(0);
    }

    private async Task<(string email, Guid id)> Register(string email, string name)
    {
        var response = await _client.PostAsync("/api/auth/register",
            Json(new
            {
                email,
                password = "Pass123!",
                firstName = name,
                lastName = name,
                userName = name
            }));

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<AuthResponse>();
        json.Should().NotBeNull();

        return (email, json!.userId);
    }

    private async Task<string> Login(string email)
    {
        var response = await _client.PostAsync("/api/auth/login",
            Json(new
            {
                email,
                password = "Pass123!",
                deviceName = "test",
                platform = "test"
            }));

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<AuthResponse>();
        json.Should().NotBeNull();

        return json!.accessToken;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);
}