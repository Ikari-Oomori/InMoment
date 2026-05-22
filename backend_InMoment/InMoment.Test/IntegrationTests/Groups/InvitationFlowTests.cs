using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;
using System.Net;
using System.Net.Http.Json;

namespace InMoment.IntegrationTests.Groups;

public sealed class InvitationFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InvitationFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Invite_And_User_Can_Accept()
    {
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var user = await Register($"user_{Guid.NewGuid():N}@test.com", $"User{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("accept-flow-group");

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invitePayload.Should().NotBeNull();
        invitePayload!.invitationId.Should().NotBeEmpty();

        var userToken = await Login(user.email);
        SetAuth(_client, userToken);

        var myInvitesResponse = await _client.GetAsync("/api/invitations/my");
        myInvitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitations = await myInvitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();
        invitations.Should().ContainSingle(x => x.GroupId == groupId);

        var invitationId = invitations!.Single(x => x.GroupId == groupId).Id;

        var acceptResponse = await _client.PostAsync(
            $"/api/invitations/{invitationId}/accept",
            null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();
        members!.Should().Contain(x => x.UserId == owner.id);
        members.Should().Contain(x => x.UserId == user.id);
    }

    [Fact]
    public async Task Owner_Cannot_Invite_User_When_Target_Privacy_Disables_GroupInvites()
    {
        var owner = await Register($"owner_priv_{Guid.NewGuid():N}@test.com", $"OwnerPriv{Guid.NewGuid():N}");
        var user = await Register($"user_priv_{Guid.NewGuid():N}@test.com", $"UserPriv{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("privacy-invite-group");

        var userToken = await Login(user.email);
        SetAuth(_client, userToken);

        var patchPrivacy = await _client.PatchAsync("/api/privacy", Json(new
        {
            allowFriendRequestsFrom = 1,
            allowGroupInvitesFrom = 3,
            discoverableByContacts = true,
            discoverableBySearch = true
        }));

        patchPrivacy.StatusCode.Should().Be(HttpStatusCode.NoContent);

        SetAuth(_client, ownerToken);

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Can_Reject_Invitation()
    {
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var user = await Register($"user_{Guid.NewGuid():N}@test.com", $"User{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("reject-flow-group");

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userToken = await Login(user.email);
        SetAuth(_client, userToken);

        var myInvitesResponse = await _client.GetAsync("/api/invitations/my");
        myInvitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitations = await myInvitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();
        invitations.Should().ContainSingle(x => x.GroupId == groupId);

        var invitationId = invitations!.Single(x => x.GroupId == groupId).Id;

        var rejectResponse = await _client.PostAsync(
            $"/api/invitations/{invitationId}/reject",
            null);

        rejectResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();
        members!.Should().ContainSingle(x => x.UserId == owner.id);
        members.Should().NotContain(x => x.UserId == user.id);

        var myInvitesAfterReject = await _client.GetAsync("/api/invitations/my");
        myInvitesAfterReject.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitationsAfterReject = await myInvitesAfterReject.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitationsAfterReject.Should().NotBeNull();
        invitationsAfterReject!.Should().NotContain(x => x.GroupId == groupId);
    }

    [Fact]
    public async Task Inviter_Can_Cancel_Pending_Invitation()
    {
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var user = await Register($"user_{Guid.NewGuid():N}@test.com", $"User{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("cancel-flow-group");

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invitePayload.Should().NotBeNull();

        var cancelResponse = await _client.PostAsync(
            $"/api/invitations/{invitePayload!.invitationId}/cancel",
            null);

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var userToken = await Login(user.email);
        SetAuth(_client, userToken);

        var myInvitesResponse = await _client.GetAsync("/api/invitations/my");
        myInvitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitations = await myInvitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();
        invitations!.Should().NotContain(x => x.GroupId == groupId);
    }

    [Fact]
    public async Task Owner_Can_Cancel_Pending_Invitation_Created_By_Admin()
    {
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var admin = await Register($"admin_{Guid.NewGuid():N}@test.com", $"Admin{Guid.NewGuid():N}");
        var user = await Register($"user_{Guid.NewGuid():N}@test.com", $"User{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("owner-cancel-admin-invite-group");

        await InviteAndAccept(groupId, admin.email, ownerToken);

        var makeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{admin.id}/make-admin",
            null);

        makeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminToken = await Login(admin.email);
        SetAuth(_client, adminToken);

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invitePayload.Should().NotBeNull();

        ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var cancelResponse = await _client.PostAsync(
            $"/api/invitations/{invitePayload!.invitationId}/cancel",
            null);

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var userToken = await Login(user.email);
        SetAuth(_client, userToken);

        var myInvitesResponse = await _client.GetAsync("/api/invitations/my");
        myInvitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitations = await myInvitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();
        invitations!.Should().NotContain(x => x.GroupId == groupId);
    }

    [Fact]
    public async Task NonOwner_Cannot_Cancel_Invitation_They_Did_Not_Create()
    {
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var admin = await Register($"admin_{Guid.NewGuid():N}@test.com", $"Admin{Guid.NewGuid():N}");
        var member = await Register($"member_{Guid.NewGuid():N}@test.com", $"Member{Guid.NewGuid():N}");
        var user = await Register($"user_{Guid.NewGuid():N}@test.com", $"User{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("forbidden-cancel-group");

        await InviteAndAccept(groupId, admin.email, ownerToken);
        await InviteAndAccept(groupId, member.email, ownerToken);

        var makeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{admin.id}/make-admin",
            null);

        makeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminToken = await Login(admin.email);
        SetAuth(_client, adminToken);

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invitePayload.Should().NotBeNull();

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var cancelResponse = await _client.PostAsync(
            $"/api/invitations/{invitePayload!.invitationId}/cancel",
            null);

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invitePayload.Should().NotBeNull();

        var invitedToken = await Login(invitedEmail);
        SetAuth(_client, invitedToken);

        var myInvitesResponse = await _client.GetAsync("/api/invitations/my");
        myInvitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitations = await myInvitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();

        var invitationId = invitations!.Single(x => x.GroupId == groupId).Id;

        var acceptResponse = await _client.PostAsync(
            $"/api/invitations/{invitationId}/accept",
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

    private sealed record InviteUserResponse(Guid invitationId);

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
        DateTime JoinedAtUtc);
}