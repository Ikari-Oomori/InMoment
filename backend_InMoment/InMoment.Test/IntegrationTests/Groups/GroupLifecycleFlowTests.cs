using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Groups;

public sealed class GroupLifecycleFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GroupLifecycleFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Cannot_Leave_Group_While_Still_Having_Other_Members()
    {
        var owner = await Register($"owner_leave_{Guid.NewGuid():N}@test.com", $"ownerleave{Guid.NewGuid():N}");
        var member = await Register($"member_leave_{Guid.NewGuid():N}@test.com", $"memberleave{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("leave-blocked-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var leaveResponse = await _client.PostAsync($"/api/groups/{groupId}/leave", null);

        leaveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();
        members!.Should().Contain(x => x.UserId == owner.id && x.IsOwner);
        members.Should().Contain(x => x.UserId == member.id);
    }

    [Fact]
    public async Task Member_Can_Leave_Group_And_Loses_Access_To_Group_Data()
    {
        var owner = await Register($"owner_memberleave_{Guid.NewGuid():N}@test.com", $"ownerml{Guid.NewGuid():N}");
        var member = await Register($"member_memberleave_{Guid.NewGuid():N}@test.com", $"memberml{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("member-leave-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var leaveResponse = await _client.PostAsync($"/api/groups/{groupId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membersAfterLeaveResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersAfterLeaveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var ownerMembersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        ownerMembersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await ownerMembersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();
        members!.Should().ContainSingle(x => x.UserId == owner.id);
        members.Should().NotContain(x => x.UserId == member.id);
    }

    [Fact]
    public async Task Owner_Can_Transfer_Ownership_And_Then_Leave_Group()
    {
        var owner = await Register($"owner_transferleave_{Guid.NewGuid():N}@test.com", $"ownertrl{Guid.NewGuid():N}");
        var member = await Register($"member_transferleave_{Guid.NewGuid():N}@test.com", $"membertrl{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("transfer-then-leave-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var transferResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/transfer-ownership",
            Json(new { newOwnerUserId = member.id }));

        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var leaveResponse = await _client.PostAsync($"/api/groups/{groupId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();
        members!.Should().ContainSingle(x => x.UserId == member.id && x.IsOwner);
        members.Should().NotContain(x => x.UserId == owner.id);
    }

    [Fact]
    public async Task Admin_Cannot_Remove_Himself_And_Should_Use_Leave()
    {
        var owner = await Register($"owner_selfremove_{Guid.NewGuid():N}@test.com", $"ownersr{Guid.NewGuid():N}");
        var admin = await Register($"admin_selfremove_{Guid.NewGuid():N}@test.com", $"adminsr{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("admin-self-remove-group");
        await InviteAndAccept(groupId, admin.email, ownerToken);

        var makeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{admin.id}/make-admin",
            null);

        makeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminToken = await Login(admin.email);
        SetAuth(_client, adminToken);

        var removeSelfResponse = await _client.DeleteAsync($"/api/groups/{groupId}/members/{admin.id}");
        removeSelfResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var leaveResponse = await _client.PostAsync($"/api/groups/{groupId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Owner_Can_Remove_Admin_Role_And_User_Becomes_Regular_Member()
    {
        var owner = await Register($"owner_removeadmin_{Guid.NewGuid():N}@test.com", $"ownerra{Guid.NewGuid():N}");
        var user = await Register($"user_removeadmin_{Guid.NewGuid():N}@test.com", $"userra{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("remove-admin-role-group");
        await InviteAndAccept(groupId, user.email, ownerToken);

        var makeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{user.id}/make-admin",
            null);

        makeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var removeAdminResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{user.id}/remove-admin",
            null);

        removeAdminResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membersResponse = await _client.GetAsync($"/api/groups/{groupId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await membersResponse.Content.ReadFromJsonAsync<List<GroupMemberDto>>();
        members.Should().NotBeNull();

        var target = members!.Single(x => x.UserId == user.id);
        target.IsOwner.Should().BeFalse();
        target.IsAdmin.Should().BeFalse();
        target.Role.Should().Be(3); // Member
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
            deviceName = "test",
            platform = "test"
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

        var json = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        json.Should().NotBeNull();

        return json!.groupId;
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