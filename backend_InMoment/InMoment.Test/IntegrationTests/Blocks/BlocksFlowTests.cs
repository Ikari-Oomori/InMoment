using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Blocks;

public sealed class BlocksFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BlocksFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Block_List_And_Unblock_User()
    {
        var blocker = await Register(
            $"blocker_{Guid.NewGuid():N}@test.com",
            $"blocker_{Guid.NewGuid():N}");

        var target = await Register(
            $"target_{Guid.NewGuid():N}@test.com",
            $"target_{Guid.NewGuid():N}");

        var blockerToken = await Login(blocker.email);
        SetAuth(_client, blockerToken);

        var blockResponse = await _client.PostAsync($"/api/blocks/{target.userId}", null);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterBlockResponse = await _client.GetAsync("/api/blocks");
        listAfterBlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var blockedUsers = await listAfterBlockResponse.Content.ReadFromJsonAsync<List<BlockedUserDto>>();
        blockedUsers.Should().NotBeNull();
        blockedUsers!.Should().ContainSingle();
        blockedUsers[0].UserId.Should().Be(target.userId);
        blockedUsers[0].UserName.Should().Be(target.userName);

        var unblockResponse = await _client.DeleteAsync($"/api/blocks/{target.userId}");
        unblockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterUnblockResponse = await _client.GetAsync("/api/blocks");
        listAfterUnblockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var blockedUsersAfterUnblock = await listAfterUnblockResponse.Content.ReadFromJsonAsync<List<BlockedUserDto>>();
        blockedUsersAfterUnblock.Should().NotBeNull();
        blockedUsersAfterUnblock!.Should().BeEmpty();
    }

    [Fact]
    public async Task Blocks_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var targetUserId = Guid.NewGuid();

        var listResponse = await _client.GetAsync("/api/blocks");
        var blockResponse = await _client.PostAsync($"/api/blocks/{targetUserId}", null);
        var unblockResponse = await _client.DeleteAsync($"/api/blocks/{targetUserId}");

        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unblockResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string email, Guid userId, string userName)> Register(string email, string userName)
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

        return (email, auth!.userId, userName);
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

    private sealed record BlockedUserDto(
        Guid UserId,
        string UserName,
        string FirstName,
        string LastName,
        string? ProfilePhotoUrl,
        DateTime BlockedAtUtc);
}