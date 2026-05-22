using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Groups;

public sealed class GroupSettingsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GroupSettingsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Get_Update_And_Set_Avatar()
    {
        var user = await Register($"gs_{Guid.NewGuid():N}@test.com", $"gs_{Guid.NewGuid():N}");
        var token = await Login(user.email);
        SetAuth(_client, token);

        var groupId = await CreateGroup("Initial");

        // GET
        var get = await _client.GetAsync($"/api/groups/{groupId}/settings");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        // UPDATE
        var update = await _client.PatchAsync(
            $"/api/groups/{groupId}/settings",
            Json(new { name = "Updated", description = "Desc" }));

        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await update.Content.ReadFromJsonAsync<GroupSettingsDto>();
        dto!.Name.Should().Be("Updated");

        // AVATAR
        var avatar = await _client.PostAsync(
            $"/api/groups/{groupId}/avatar",
            Json(new { avatarUrl = "url" }));

        avatar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task NonOwner_Cannot_Update_Or_Set_Avatar()
    {
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"own_{Guid.NewGuid():N}");
        var member = await Register($"member_{Guid.NewGuid():N}@test.com", $"mem_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Test");

        await InviteAndAccept(groupId, member.email, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var update = await _client.PatchAsync(
            $"/api/groups/{groupId}/settings",
            Json(new { name = "fail", description = "fail" }));

        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var avatar = await _client.PostAsync(
            $"/api/groups/{groupId}/avatar",
            Json(new { avatarUrl = "fail" }));

        avatar.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
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

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.accessToken;
    }

    private async Task<Guid> CreateGroup(string name)
    {
        var response = await _client.PostAsync("/api/groups", Json(new { name }));
        var dto = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        return dto!.groupId;
    }

    private async Task InviteAndAccept(Guid groupId, string invitedEmail, string ownerToken)
    {
        SetAuth(_client, ownerToken);

        await _client.PostAsync($"/api/groups/{groupId}/invite", Json(new { login = invitedEmail }));

        var invitedToken = await Login(invitedEmail);
        SetAuth(_client, invitedToken);

        var invites = await _client.GetFromJsonAsync<List<InvitationDto>>("/api/invitations/my");
        var invitation = invites!.Single(x => x.GroupId == groupId);

        await _client.PostAsync($"/api/invitations/{invitation.Id}/accept", null);

        SetAuth(_client, ownerToken);
    }

    private sealed record AuthResponse(Guid userId, string accessToken, string refreshToken, DateTime refreshTokenExpiresAtUtc);
    private sealed record CreateGroupResponse(Guid groupId);
    private sealed record InvitationDto(Guid Id, Guid GroupId, Guid InvitedByUserId, DateTime CreatedAt);

    private sealed record GroupSettingsDto(
        Guid Id,
        string Name,
        string? Description,
        string? AvatarUrl,
        Guid OwnerId,
        DateTime CreatedAt);
}