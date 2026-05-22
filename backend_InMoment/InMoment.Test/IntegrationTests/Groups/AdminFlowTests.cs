using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;
using System.Net;
using System.Net.Http.Json;

namespace InMoment.IntegrationTests.Groups;

public sealed class AdminFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdminFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Promote_User_To_Admin()
    {
        var owner = await Register("owner@test.com", "Owner");
        var user = await Register("user@test.com", "User");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("test group");

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteUserResponse>();
        invitePayload.Should().NotBeNull();

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

        ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var promoteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{user.id}/make-admin",
            null);

        promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();

        var promotedUser = members!.Single(x => x.UserId == user.id);
        promotedUser.IsAdmin.Should().BeTrue();
        promotedUser.IsOwner.Should().BeFalse();
        promotedUser.Role.Should().Be(2); // Admin
    }

    [Fact]
    public async Task Owner_Can_Transfer_Ownership()
    {
        var owner = await Register("owner2@test.com", "Owner2");
        var user = await Register("user2@test.com", "User2");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("ownership group");

        var inviteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = user.email }));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userToken = await Login(user.email);
        SetAuth(_client, userToken);

        var invitesResponse = await _client.GetAsync("/api/invitations/my");
        invitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitations = await invitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();
        invitations.Should().ContainSingle(x => x.GroupId == groupId);

        var invitationId = invitations!.Single(x => x.GroupId == groupId).Id;

        var acceptResponse = await _client.PostAsync(
            $"/api/invitations/{invitationId}/accept",
            null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var response = await _client.PostAsync(
            $"/api/groups/{groupId}/transfer-ownership",
            Json(new { newOwnerUserId = user.id }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();

        var newOwner = members!.Single(x => x.UserId == user.id);
        newOwner.IsOwner.Should().BeTrue();
        newOwner.IsAdmin.Should().BeFalse();
        newOwner.Role.Should().Be(1); // Owner

        var oldOwner = members!.Single(x => x.UserId == owner.id);
        oldOwner.IsOwner.Should().BeFalse();
        oldOwner.IsAdmin.Should().BeTrue();
        oldOwner.Role.Should().Be(2); // Admin
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

    private async Task<Guid> CreateGroup(string name)
    {
        var response = await _client.PostAsync("/api/groups",
            Json(new { name }));

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        json.Should().NotBeNull();

        return json!.groupId;
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
        bool IsOwner,
        bool IsAdmin);


    [Fact]
    public async Task Admin_Can_Remove_Regular_Member()
    {
        var owner = await Register("owner3@test.com", "Owner3");
        var adminUser = await Register("admin3@test.com", "Admin3");
        var memberUser = await Register("member3@test.com", "Member3");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("remove-member-group");

        await InviteAndAccept(groupId, adminUser.email, ownerToken);
        await InviteAndAccept(groupId, memberUser.email, ownerToken);

        var makeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{adminUser.id}/make-admin",
            null);

        makeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminToken = await Login(adminUser.email);
        SetAuth(_client, adminToken);

        var removeResponse = await _client.DeleteAsync(
            $"/api/groups/{groupId}/members/{memberUser.id}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();

        members!.Should().Contain(x => x.UserId == owner.id);
        members.Should().Contain(x => x.UserId == adminUser.id);
        members.Should().NotContain(x => x.UserId == memberUser.id);
    }

    [Fact]
    public async Task Admin_Cannot_Remove_Owner()
    {
        var owner = await Register("owner4@test.com", "Owner4");
        var adminUser = await Register("admin4@test.com", "Admin4");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("cannot-remove-owner-group");

        await InviteAndAccept(groupId, adminUser.email, ownerToken);

        var makeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{adminUser.id}/make-admin",
            null);

        makeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminToken = await Login(adminUser.email);
        SetAuth(_client, adminToken);

        var removeResponse = await _client.DeleteAsync(
            $"/api/groups/{groupId}/members/{owner.id}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();

        members!.Should().Contain(x => x.UserId == owner.id && x.IsOwner);
        members.Should().Contain(x => x.UserId == adminUser.id && x.IsAdmin);
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

        var invitations = await invitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invitations.Should().NotBeNull();
        invitations.Should().ContainSingle(x => x.GroupId == groupId);

        var invitationId = invitations!.Single(x => x.GroupId == groupId).Id;

        var acceptResponse = await _client.PostAsync(
            $"/api/invitations/{invitationId}/accept",
            null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        SetAuth(_client, inviterToken);
    }

    [Fact]
    public async Task Admin_Cannot_Remove_Other_Admin()
    {
        var owner = await Register("owner5@test.com", "Owner5");
        var admin1 = await Register("admin5a@test.com", "Admin5A");
        var admin2 = await Register("admin5b@test.com", "Admin5B");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("admins-group");

        await InviteAndAccept(groupId, admin1.email, ownerToken);
        await InviteAndAccept(groupId, admin2.email, ownerToken);

        var makeAdmin1Response = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{admin1.id}/make-admin",
            null);

        makeAdmin1Response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var makeAdmin2Response = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{admin2.id}/make-admin",
            null);

        makeAdmin2Response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var admin1Token = await Login(admin1.email);
        SetAuth(_client, admin1Token);

        var removeResponse = await _client.DeleteAsync(
            $"/api/groups/{groupId}/members/{admin2.id}");

        removeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();

        members!.Should().Contain(x => x.UserId == owner.id && x.IsOwner);
        members.Should().Contain(x => x.UserId == admin1.id && x.IsAdmin);
        members.Should().Contain(x => x.UserId == admin2.id && x.IsAdmin);
    }
}