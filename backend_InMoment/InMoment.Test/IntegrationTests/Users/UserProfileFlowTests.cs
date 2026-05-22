using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Users;

public sealed class UserProfileFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserProfileFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Get_And_Update_Profile()
    {
        var email = $"me_{Guid.NewGuid():N}@test.com";
        var userName = $"me_{Guid.NewGuid():N}";
        var phone = UniquePhone("100");

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var getBeforeResponse = await _client.GetAsync("/api/users/me");
        getBeforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meBefore = await getBeforeResponse.Content.ReadFromJsonAsync<MeDto>();
        meBefore.Should().NotBeNull();
        meBefore!.Email.Should().Be(email);
        meBefore.UserName.Should().Be(userName);
        meBefore.FirstName.Should().Be("Anna");
        meBefore.LastName.Should().Be("Petrova");
        meBefore.PhoneNumber.Should().BeNull();
        meBefore.ProfilePhotoUrl.Should().BeNull();
        meBefore.ActiveGroupId.Should().BeNull();
        meBefore.GroupsCount.Should().Be(0);
        meBefore.PendingInvitationsCount.Should().Be(0);
        meBefore.Groups.Should().BeEmpty();
        meBefore.PendingInvitations.Should().BeEmpty();

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me")
        {
            Content = Json(new
            {
                userName = $"{userName}_new",
                firstName = "Elena",
                lastName = "Sidorova",
                phoneNumber = phone
            })
        };

        var patchResponse = await _client.SendAsync(patchRequest);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await patchResponse.Content.ReadFromJsonAsync<UpdatedMeDto>();
        updated.Should().NotBeNull();
        updated!.Email.Should().Be(email);
        updated.PhoneNumber.Should().Be(NormalizePhone(phone));
        updated.UserName.Should().Be($"{userName}_new");
        updated.FirstName.Should().Be("Elena");
        updated.LastName.Should().Be("Sidorova");

        var getAfterResponse = await _client.GetAsync("/api/users/me");
        getAfterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meAfter = await getAfterResponse.Content.ReadFromJsonAsync<MeDto>();
        meAfter.Should().NotBeNull();
        meAfter!.UserName.Should().Be($"{userName}_new");
        meAfter.FirstName.Should().Be("Elena");
        meAfter.LastName.Should().Be("Sidorova");
        meAfter.PhoneNumber.Should().Be(NormalizePhone(phone));
        meAfter.GroupsCount.Should().Be(0);
        meAfter.PendingInvitationsCount.Should().Be(0);
        meAfter.Groups.Should().BeEmpty();
        meAfter.PendingInvitations.Should().BeEmpty();
    }

    [Fact]
    public async Task User_Can_Clear_PhoneNumber_In_Profile()
    {
        var email = $"clear_phone_{Guid.NewGuid():N}@test.com";
        var userName = $"clearphone_{Guid.NewGuid():N}";
        var phone = UniquePhone("200");

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var setPhoneRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me")
        {
            Content = Json(new
            {
                phoneNumber = phone
            })
        };

        var setPhoneResponse = await _client.SendAsync(setPhoneRequest);
        setPhoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedWithPhone = await setPhoneResponse.Content.ReadFromJsonAsync<UpdatedMeDto>();
        updatedWithPhone.Should().NotBeNull();
        updatedWithPhone!.PhoneNumber.Should().Be(NormalizePhone(phone));

        var clearPhoneRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me")
        {
            Content = Json(new
            {
                phoneNumber = "   "
            })
        };

        var clearPhoneResponse = await _client.SendAsync(clearPhoneRequest);
        clearPhoneResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedCleared = await clearPhoneResponse.Content.ReadFromJsonAsync<UpdatedMeDto>();
        updatedCleared.Should().NotBeNull();
        updatedCleared!.PhoneNumber.Should().BeNull();

        var getAfterClearResponse = await _client.GetAsync("/api/users/me");
        getAfterClearResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meAfterClear = await getAfterClearResponse.Content.ReadFromJsonAsync<MeDto>();
        meAfterClear.Should().NotBeNull();
        meAfterClear!.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task User_Register_And_Update_Phone_ShouldUse_Canonical_Normalized_Format()
    {
        var email = $"phone_norm_{Guid.NewGuid():N}@test.com";
        var userName = $"phonenorm_{Guid.NewGuid():N}";
        var registerPhone = UniquePhone("300");
        var updatePhoneSameNumberDifferentFormat = registerPhone
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Insert(3, " ")
            .Insert(7, " ");

        var registerResponse = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName = "Anna",
            lastName = "Petrova",
            userName,
            phoneNumber = registerPhone
        }));

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        SetAuth(_client, auth!.accessToken);

        var getAfterRegisterResponse = await _client.GetAsync("/api/users/me");
        getAfterRegisterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meAfterRegister = await getAfterRegisterResponse.Content.ReadFromJsonAsync<MeDto>();
        meAfterRegister.Should().NotBeNull();
        meAfterRegister!.PhoneNumber.Should().Be(NormalizePhone(registerPhone));

        var updateRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me")
        {
            Content = Json(new
            {
                phoneNumber = updatePhoneSameNumberDifferentFormat
            })
        };

        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<UpdatedMeDto>();
        updated.Should().NotBeNull();
        updated!.PhoneNumber.Should().Be(NormalizePhone(registerPhone));

        var getAfterUpdateResponse = await _client.GetAsync("/api/users/me");
        getAfterUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meAfterUpdate = await getAfterUpdateResponse.Content.ReadFromJsonAsync<MeDto>();
        meAfterUpdate.Should().NotBeNull();
        meAfterUpdate!.PhoneNumber.Should().Be(NormalizePhone(registerPhone));
    }

    [Fact]
    public async Task GetMe_ShouldReturn_GroupsCount_PendingInvitationsCount_GroupsPreview_And_PendingInvitationsPreview()
    {
        var ownerEmail = $"owner_profile_{Guid.NewGuid():N}@test.com";
        var ownerUserName = $"ownerprofile_{Guid.NewGuid():N}";
        var owner = await Register(ownerEmail, ownerUserName, "Owner", "User");

        var memberEmail = $"member_profile_{Guid.NewGuid():N}@test.com";
        var memberUserName = $"memberprofile_{Guid.NewGuid():N}";
        await Register(memberEmail, memberUserName, "Member", "User");

        var ownerToken = await Login(ownerEmail);
        SetAuth(_client, ownerToken);

        var group1Id = await CreateGroup("Profile Group 1");
        var group2Id = await CreateGroup("Profile Group 2");

        await Invite(group1Id, memberEmail);
        await Invite(group2Id, memberEmail);

        var memberToken = await Login(memberEmail);
        SetAuth(_client, memberToken);

        var meResponse = await _client.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();
        me!.GroupsCount.Should().Be(0);
        me.PendingInvitationsCount.Should().Be(2);
        me.Groups.Should().BeEmpty();
        me.PendingInvitations.Should().HaveCount(2);
        me.PendingInvitations.Should().OnlyContain(x => x.InvitedByUserId == owner.userId);
        me.PendingInvitations.Select(x => x.GroupId).Should().BeEquivalentTo(new[] { group1Id, group2Id });

        var invitesResponse = await _client.GetAsync("/api/invitations/my");
        invitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invites = await invitesResponse.Content.ReadFromJsonAsync<List<InvitationDto>>();
        invites.Should().NotBeNull();
        invites!.Should().HaveCount(2);

        var inviteForGroup1 = invites.Single(x => x.GroupId == group1Id);

        var acceptResponse = await _client.PostAsync($"/api/invitations/{inviteForGroup1.Id}/accept", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfterResponse = await _client.GetAsync("/api/users/me");
        meAfterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meAfter = await meAfterResponse.Content.ReadFromJsonAsync<MeDto>();
        meAfter.Should().NotBeNull();
        meAfter!.GroupsCount.Should().Be(1);
        meAfter.PendingInvitationsCount.Should().Be(1);
        meAfter.Groups.Should().HaveCount(1);
        meAfter.Groups[0].Id.Should().Be(group1Id);
        meAfter.Groups[0].Name.Should().Be("Profile Group 1");
        meAfter.Groups[0].IsActiveGroup.Should().BeFalse();

        meAfter.PendingInvitations.Should().HaveCount(1);
        meAfter.PendingInvitations[0].GroupId.Should().Be(group2Id);
        meAfter.PendingInvitations[0].GroupName.Should().Be("Profile Group 2");
        meAfter.PendingInvitations[0].InvitedByUserId.Should().Be(owner.userId);
        meAfter.PendingInvitations[0].InvitedByUserName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetMe_ShouldReturn_ActiveGroup_First_In_GroupsPreview()
    {
        var email = $"profile_groups_{Guid.NewGuid():N}@test.com";
        var userName = $"profilegroups_{Guid.NewGuid():N}";
        await Register(email, userName, "Profile", "Groups");

        var token = await Login(email);
        SetAuth(_client, token);

        var groupB = await CreateGroup("B Group");
        var groupA = await CreateGroup("A Group");

        var setActiveResponse = await _client.PatchAsync(
            "/api/users/me/active-group",
            Json(new { groupId = groupB }));

        setActiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await _client.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();
        me!.GroupsCount.Should().Be(2);
        me.Groups.Should().HaveCount(2);

        me.Groups[0].Id.Should().Be(groupB);
        me.Groups[0].Name.Should().Be("B Group");
        me.Groups[0].IsActiveGroup.Should().BeTrue();

        me.Groups[1].Id.Should().Be(groupA);
        me.Groups[1].Name.Should().Be("A Group");
        me.Groups[1].IsActiveGroup.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMe_ShouldReturnBadRequest_WhenNicknameAlreadyUsed()
    {
        var takenEmail = $"taken_{Guid.NewGuid():N}@test.com";
        var takenUserName = $"taken_{Guid.NewGuid():N}";
        await Register(takenEmail, takenUserName, "Taken", "User");

        var email = $"profile_{Guid.NewGuid():N}@test.com";
        var userName = $"profile_{Guid.NewGuid():N}";
        await Register(email, userName, "Profile", "User");

        var token = await Login(email);
        SetAuth(_client, token);

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me")
        {
            Content = Json(new
            {
                userName = takenUserName,
                firstName = "New",
                lastName = "Name"
            })
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_Can_Set_Profile_Photo_And_See_It_In_Me()
    {
        var email = $"photo_{Guid.NewGuid():N}@test.com";
        var userName = $"photo_{Guid.NewGuid():N}";

        await Register(email, userName, "Photo", "User");
        var token = await Login(email);

        SetAuth(_client, token);

        var setPhotoResponse = await _client.PostAsync("/api/users/me/profile-photo", Json(new
        {
            url = "https://cdn.example.com/profiles/photo-user.jpg"
        }));

        setPhotoResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getMeResponse = await _client.GetAsync("/api/users/me");
        getMeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await getMeResponse.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();
        me!.ProfilePhotoUrl.Should().Be("https://cdn.example.com/profiles/photo-user.jpg");
        me.GroupsCount.Should().Be(0);
        me.PendingInvitationsCount.Should().Be(0);
        me.Groups.Should().BeEmpty();
        me.PendingInvitations.Should().BeEmpty();
    }

    [Fact]
    public async Task User_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var getResponse = await _client.GetAsync("/api/users/me");

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me")
        {
            Content = Json(new
            {
                userName = "new_name",
                firstName = "New",
                lastName = "Name"
            })
        };

        var patchResponse = await _client.SendAsync(patchRequest);

        var photoResponse = await _client.PostAsync("/api/users/me/profile-photo", Json(new
        {
            url = "https://cdn.example.com/profiles/noauth.jpg"
        }));

        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        photoResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string email, Guid userId, string userName)> Register(string email, string userName, string firstName, string lastName)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName,
            lastName,
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

    private async Task<Guid> CreateGroup(string name)
    {
        var response = await _client.PostAsync("/api/groups", Json(new { name }));
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        dto.Should().NotBeNull();

        return dto!.groupId;
    }

    private async Task Invite(Guid groupId, string invitedEmail)
    {
        var response = await _client.PostAsync(
            $"/api/groups/{groupId}/invite",
            Json(new { login = invitedEmail }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string UniquePhone(string prefix)
    {
        var tail = Guid.NewGuid().ToString("N")[..8];
        return $"+49 {prefix} {tail[..3]} {tail[3..6]} {tail[6..8]}";
    }

    private static string NormalizePhone(string raw)
        => "+" + new string(raw.Where(char.IsDigit).ToArray());

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

    private sealed record MyGroupPreviewDto(
        Guid Id,
        string Name,
        string? AvatarUrl,
        bool IsActiveGroup);

    private sealed record MyPendingInvitationPreviewDto(
        Guid InvitationId,
        Guid GroupId,
        string GroupName,
        string? GroupAvatarUrl,
        Guid InvitedByUserId,
        string InvitedByUserName,
        string? InvitedByUserProfilePhotoUrl,
        DateTime CreatedAt);

    private sealed record MeDto(
        Guid Id,
        string Email,
        string UserName,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? ProfilePhotoUrl,
        Guid? ActiveGroupId,
        DateTime CreatedAt,
        int GroupsCount,
        int PendingInvitationsCount,
        IReadOnlyList<MyGroupPreviewDto> Groups,
        IReadOnlyList<MyPendingInvitationPreviewDto> PendingInvitations);

    private sealed record UpdatedMeDto(
        Guid Id,
        string Email,
        string UserName,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? ProfilePhotoUrl,
        Guid? ActiveGroupId,
        DateTime CreatedAt);
}