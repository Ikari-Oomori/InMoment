using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Groups;

public sealed class InviteCodesFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InviteCodesFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Create_Code_And_Another_User_Can_Join_By_Code()
    {
        var owner = await Register(
            $"owner_code_{Guid.NewGuid():N}@test.com",
            $"ownercode_{Guid.NewGuid():N}");

        var joiner = await Register(
            $"joiner_code_{Guid.NewGuid():N}@test.com",
            $"joinercode_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Invite Code Group");

        var createResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite-code",
            Json(new
            {
                groupId = Guid.Empty,
                maxUses = 5,
                expireHours = 24
            }));

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var code = (await createResponse.Content.ReadAsStringAsync()).Trim();
        code.Should().NotBeNullOrWhiteSpace();
        code.Should().HaveLength(8);
        code.Should().MatchRegex("^[A-Z0-9]{8}$");

        var joinerToken = await Login(joiner.email);
        SetAuth(_client, joinerToken);

        var joinResponse = await _client.PostAsync(
            "/api/groups/join-by-code",
            Json(new
            {
                code
            }));

        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();
        members!.Should().Contain(x => x.UserId == joiner.id);
    }

    [Fact]
    public async Task RegularMember_Cannot_Create_Invite_Code()
    {
        var owner = await Register(
            $"owner_code2_{Guid.NewGuid():N}@test.com",
            $"ownercode2_{Guid.NewGuid():N}");

        var member = await Register(
            $"member_code2_{Guid.NewGuid():N}@test.com",
            $"membercode2_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Invite Code Group 2");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var createResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite-code",
            Json(new
            {
                groupId = Guid.Empty,
                maxUses = 1,
                expireHours = 1
            }));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Join_By_Invalid_Code_Returns_NotFound()
    {
        var user = await Register(
            $"invalid_code_{Guid.NewGuid():N}@test.com",
            $"invalidcode_{Guid.NewGuid():N}");

        var token = await Login(user.email);
        SetAuth(_client, token);

        var response = await _client.PostAsync(
            "/api/groups/join-by-code",
            Json(new
            {
                code = "ZZZZZZZZ"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Join_By_Empty_Code_Returns_BadRequest()
    {
        var user = await Register(
            $"empty_code_{Guid.NewGuid():N}@test.com",
            $"emptycode_{Guid.NewGuid():N}");

        var token = await Login(user.email);
        SetAuth(_client, token);

        var response = await _client.PostAsync(
            "/api/groups/join-by-code",
            Json(new
            {
                code = "   "
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private async Task InviteAndAccept(Guid groupId, string invitedEmail, string inviterToken)
    {
        SetAuth(_client, inviterToken);

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = invitedEmail }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitedToken = await Login(invitedEmail);
        SetAuth(_client, invitedToken);

        var invitesResponse = await _client.GetAsync("/api/invitations/my");
        invitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invites = await invitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invites.Should().NotBeNull();

        var invitation = invites!.Single(x => x.GroupId == groupId);

        var acceptResponse = await _client.PostAsync(
            $"/api/invitations/{invitation.Id}/accept",
            null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        SetAuth(_client, inviterToken);
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record CreateGroupResponse(Guid groupId);

    private sealed record InvitationDto(
        Guid Id,
        Guid GroupId,
        Guid InvitedByUserId,
        DateTime CreatedAt);

    private sealed record GroupMemberDto(
        Guid UserId,
        string UserName,
        string FirstName,
        string LastName,
        int Role,
        bool IsOwner,
        bool IsAdmin);
}