using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Accounts;

public sealed class AccountFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Get_Data_Summary_And_Deactivate_Account()
    {
        var email = $"account_{Guid.NewGuid():N}@test.com";
        var userName = $"accountuser_{Guid.NewGuid():N}";

        var auth = await Register(email, userName);
        var token = await Login(email);

        SetAuth(_client, token);

        var summaryBeforeResponse = await _client.GetAsync("/api/account/data-summary");
        summaryBeforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryBefore = await summaryBeforeResponse.Content.ReadFromJsonAsync<AccountDataSummaryDto>();
        summaryBefore.Should().NotBeNull();
        summaryBefore!.UserId.Should().Be(auth.userId);
        summaryBefore.IsActive.Should().BeTrue();

        var deactivateResponse = await _client.PostAsync("/api/account/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var summaryAfterResponse = await _client.GetAsync("/api/account/data-summary");
        summaryAfterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryAfter = await summaryAfterResponse.Content.ReadFromJsonAsync<AccountDataSummaryDto>();
        summaryAfter.Should().NotBeNull();
        summaryAfter!.UserId.Should().Be(auth.userId);
        summaryAfter.IsActive.Should().BeFalse();

        var loginAfterDeactivateResponse = await _client.PostAsync("/api/auth/login", Json(new
        {
            email,
            password = "Pass123!",
            deviceName = "tests",
            platform = "tests"
        }));

        loginAfterDeactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authAfterReactivate = await loginAfterDeactivateResponse.Content.ReadFromJsonAsync<AuthResponse>();
        authAfterReactivate.Should().NotBeNull();

        SetAuth(_client, authAfterReactivate!.accessToken);

        var summaryAfterReactivateResponse = await _client.GetAsync("/api/account/data-summary");
        summaryAfterReactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryAfterReactivate =
            await summaryAfterReactivateResponse.Content.ReadFromJsonAsync<AccountDataSummaryDto>();

        summaryAfterReactivate.Should().NotBeNull();
        summaryAfterReactivate!.UserId.Should().Be(auth.userId);
        summaryAfterReactivate.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task User_Can_Get_Data_Summary_And_Permanently_Delete_Account()
    {
        var email = $"account_perm_{Guid.NewGuid():N}@test.com";
        var userName = $"accountperm_{Guid.NewGuid():N}";

        var auth = await Register(email, userName);
        var token = await Login(email);

        SetAuth(_client, token);

        var summaryBeforeResponse = await _client.GetAsync("/api/account/data-summary");
        summaryBeforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaryBefore = await summaryBeforeResponse.Content.ReadFromJsonAsync<AccountDataSummaryDto>();
        summaryBefore.Should().NotBeNull();
        summaryBefore!.UserId.Should().Be(auth.userId);
        summaryBefore.IsActive.Should().BeTrue();

        var wrongRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/account/permanent")
        {
            Content = Json(new { confirmation = "WRONG" })
        };

        var wrongDeleteResponse = await _client.SendAsync(wrongRequest);
        wrongDeleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/account/permanent")
        {
            Content = Json(new { confirmation = "DELETE" })
        };

        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginAfterDeleteResponse = await _client.PostAsync("/api/auth/login", Json(new
        {
            email,
            password = "Pass123!",
            deviceName = "tests",
            platform = "tests"
        }));

        loginAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Account_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var summaryResponse = await _client.GetAsync("/api/account/data-summary");
        var deactivateResponse = await _client.PostAsync("/api/account/deactivate", null);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/account/permanent")
        {
            Content = Json(new { confirmation = "DELETE" })
        };

        var deleteResponse = await _client.SendAsync(deleteRequest);

        summaryResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<AuthResponse> Register(string email, string userName)
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

        return auth!;
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

    private sealed record AccountDataSummaryDto(
        Guid UserId,
        bool IsActive,
        int GroupsCount,
        int OwnedGroupsCount,
        int PhotosCount,
        int CommentsCount,
        int ReactionsCount,
        int FriendshipsCount,
        int ActiveSessionsCount);
}