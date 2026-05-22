using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Search;

public sealed class SearchFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SearchFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_Groups_Returns_Only_My_Groups()
    {
        var owner1 = await Register(
            $"search_groups_1_{Guid.NewGuid():N}@test.com",
            $"searchgroups1_{Guid.NewGuid():N}");

        var owner2 = await Register(
            $"search_groups_2_{Guid.NewGuid():N}@test.com",
            $"searchgroups2_{Guid.NewGuid():N}");

        var owner1Token = await Login(owner1.email);
        SetAuth(_client, owner1Token);

        await CreateGroup("Family Circle");
        await CreateGroup("Family Trips");

        var owner2Token = await Login(owner2.email);
        SetAuth(_client, owner2Token);

        await CreateGroup("Family Secret");

        SetAuth(_client, owner1Token);

        var response = await _client.GetAsync("/api/search/groups?q=family&limit=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<SearchGroupDto>>();
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.Name.Contains("Family"));
        result.Should().NotContain(x => x.Name == "Family Secret");
    }

    [Fact]
    public async Task Search_Users_Returns_Matching_Users()
    {
        var requester = await Register(
            $"search_users_req_{Guid.NewGuid():N}@test.com",
            $"searchreq_{Guid.NewGuid():N}");

        var target1 = await Register(
            $"anna_{Guid.NewGuid():N}@test.com",
            $"annasearch_{Guid.NewGuid():N}");

        var target2 = await Register(
            $"annabelle_{Guid.NewGuid():N}@test.com",
            $"annabellesearch_{Guid.NewGuid():N}");

        var token = await Login(requester.email);
        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/search/users?q=anna&limit=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<SearchUserDto>>();
        result.Should().NotBeNull();
        result!.Should().NotBeEmpty();
        result.Should().Contain(x => x.Id == target1.id);
        result.Should().Contain(x => x.Id == target2.id);
    }

    [Fact]
    public async Task Search_Users_Hides_User_When_DiscoverableBySearch_False()
    {
        var requester = await Register(
            $"search_hide_req_{Guid.NewGuid():N}@test.com",
            $"searchhidereq_{Guid.NewGuid():N}");

        var hidden = await Register(
            $"search_hide_target_{Guid.NewGuid():N}@test.com",
            $"annahidden_{Guid.NewGuid():N}");

        var hiddenToken = await Login(hidden.email);
        SetAuth(_client, hiddenToken);

        var patchPrivacy = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 1,
            allowGroupInvitesFrom = 1,
            discoverableByContacts = true,
            discoverableBySearch = false
        }));

        patchPrivacy.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var requesterToken = await Login(requester.email);
        SetAuth(_client, requesterToken);

        var response = await _client.GetAsync("/api/search/users?q=anna&limit=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<SearchUserDto>>();
        result.Should().NotBeNull();
        result!.Should().NotContain(x => x.Id == hidden.id);
    }

    [Fact]
    public async Task Mention_Users_Returns_Matching_Users()
    {
        var requester = await Register(
            $"mention_req_{Guid.NewGuid():N}@test.com",
            $"mentionreq_{Guid.NewGuid():N}");

        var target = await Register(
            $"mention_target_{Guid.NewGuid():N}@test.com",
            $"anna_mention_{Guid.NewGuid():N}");

        var token = await Login(requester.email);
        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/mentions/users?q=anna&limit=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MentionUserDto>>();
        result.Should().NotBeNull();
        result!.Should().Contain(x => x.Id == target.id);
    }

    [Fact]
    public async Task Search_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var usersResponse = await _client.GetAsync("/api/search/users?q=test");
        var groupsResponse = await _client.GetAsync("/api/search/groups?q=test");
        var mentionsResponse = await _client.GetAsync("/api/mentions/users?q=test");

        usersResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        groupsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mentionsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_Controllers_Normalize_Limit_And_Query()
    {
        var requester = await Register(
            $"search_norm_{Guid.NewGuid():N}@test.com",
            $"searchnorm_{Guid.NewGuid():N}");

        await Register(
            $"normalize_target_{Guid.NewGuid():N}@test.com",
            $"anna_normalized_{Guid.NewGuid():N}");

        var token = await Login(requester.email);
        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/search/users?q=%20anna%20&limit=999");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<SearchUserDto>>();
        result.Should().NotBeNull();
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

    private async Task<Guid> CreateGroup(string name)
    {
        var response = await _client.PostAsync("/api/groups", Json(new { name }));
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        dto.Should().NotBeNull();

        return dto!.groupId;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record CreateGroupResponse(Guid groupId);

    private sealed record SearchGroupDto(Guid Id, string Name);

    private sealed record SearchUserDto(
        Guid Id,
        string UserName,
        string DisplayName,
        string? ProfilePhotoUrl);

    private sealed record MentionUserDto(
        Guid Id,
        string UserName,
        string DisplayName,
        string? ProfilePhotoUrl);
}