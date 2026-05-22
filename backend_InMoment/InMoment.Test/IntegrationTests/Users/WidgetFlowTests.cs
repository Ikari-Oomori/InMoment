using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.Domain.Media;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Users;

public sealed class WidgetFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WidgetFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Set_Active_Group_And_Get_Widget_Data()
    {
        var owner = await Register(
            $"owner_widget_{Guid.NewGuid():N}@test.com",
            $"ownerwidget_{Guid.NewGuid():N}");

        var token = await Login(owner.email);
        SetAuth(_client, token);

        await CreateGroup("Widget Group 1");
        var group2 = await CreateGroup("Widget Group 2");

        var photoId = await PublishPhoto(group2, owner.id, token);

        SetAuth(_client, token);

        var setActiveResponse = await _client.PatchAsync(
            "/api/users/me/active-group",
            Json(new { groupId = group2 }));

        setActiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var widgetResponse = await _client.GetAsync("/api/users/me/widget");
        widgetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var widget = await widgetResponse.Content.ReadFromJsonAsync<WidgetDto>();
        widget.Should().NotBeNull();
        widget!.ActiveGroupId.Should().Be(group2);
        widget.LatestPhotoId.Should().Be(photoId);
        widget.NewReactionsCount.Should().Be(0);
    }

    [Fact]
    public async Task User_Cannot_Set_Foreign_Group_As_Active()
    {
        var owner1 = await Register(
            $"owner1_widget_{Guid.NewGuid():N}@test.com",
            $"owner1widget_{Guid.NewGuid():N}");

        var owner2 = await Register(
            $"owner2_widget_{Guid.NewGuid():N}@test.com",
            $"owner2widget_{Guid.NewGuid():N}");

        var owner1Token = await Login(owner1.email);
        var owner2Token = await Login(owner2.email);

        SetAuth(_client, owner2Token);
        var foreignGroupId = await CreateGroup("Foreign Widget Group");

        SetAuth(_client, owner1Token);

        var setActiveResponse = await _client.PatchAsync(
            "/api/users/me/active-group",
            Json(new { groupId = foreignGroupId }));

        setActiveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var widgetResponse = await _client.GetAsync("/api/users/me/widget");
        widgetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var widget = await widgetResponse.Content.ReadFromJsonAsync<WidgetDto>();
        widget.Should().NotBeNull();
        widget!.ActiveGroupId.Should().BeNull();
        widget.LatestPhotoId.Should().BeNull();
        widget.NewReactionsCount.Should().Be(0);
    }

    [Fact]
    public async Task Widget_Becomes_Empty_When_Active_Group_Is_No_Longer_Accessible()
    {
        var owner = await Register(
            $"owner_widget2_{Guid.NewGuid():N}@test.com",
            $"ownerwidget2_{Guid.NewGuid():N}");

        var member = await Register(
            $"member_widget2_{Guid.NewGuid():N}@test.com",
            $"memberwidget2_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Widget Leave Group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var setActiveResponse = await _client.PatchAsync(
            "/api/users/me/active-group",
            Json(new { groupId }));

        setActiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var leaveResponse = await _client.PostAsync($"/api/groups/{groupId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var widgetResponse = await _client.GetAsync("/api/users/me/widget");
        widgetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var widget = await widgetResponse.Content.ReadFromJsonAsync<WidgetDto>();
        widget.Should().NotBeNull();
        widget!.ActiveGroupId.Should().BeNull();
        widget.LatestPhotoId.Should().BeNull();
        widget.NewReactionsCount.Should().Be(0);
    }

    [Fact]
    public async Task Widget_Should_Return_New_Reactions_Count_For_Latest_Photo()
    {
        var owner = await Register(
            $"owner_widget3_{Guid.NewGuid():N}@test.com",
            $"ownerwidget3_{Guid.NewGuid():N}");

        var member = await Register(
            $"member_widget3_{Guid.NewGuid():N}@test.com",
            $"memberwidget3_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        var memberToken = await Login(member.email);

        SetAuth(_client, ownerToken);
        var groupId = await CreateGroup("Widget Reactions Group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        SetAuth(_client, memberToken);
        var reactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new { type = ReactionType.Heart }));

        reactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        SetAuth(_client, ownerToken);

        var setActiveResponse = await _client.PatchAsync(
            "/api/users/me/active-group",
            Json(new { groupId }));

        setActiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var widgetResponse = await _client.GetAsync("/api/users/me/widget");
        widgetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var widget = await widgetResponse.Content.ReadFromJsonAsync<WidgetDto>();
        widget.Should().NotBeNull();
        widget!.ActiveGroupId.Should().Be(groupId);
        widget.LatestPhotoId.Should().Be(photoId);
        widget.NewReactionsCount.Should().Be(1);
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

        var acceptResponse = await _client.PostAsync($"/api/invitations/{invitation.Id}/accept", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        SetAuth(_client, inviterToken);
    }

    private async Task<Guid> PublishPhoto(Guid groupId, Guid uploaderId, string token)
    {
        SetAuth(_client, token);

        var storageKey = $"groups/{groupId}/photos/{uploaderId}/{Guid.NewGuid():N}.jpg";

        var response = await _client.PostAsync(
            $"/api/groups/{groupId}/photos",
            Json(new
            {
                storageKey,
                contentType = "image/jpeg",
                sizeBytes = 1024
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var photoId = await response.Content.ReadFromJsonAsync<Guid>();
        photoId.Should().NotBe(Guid.Empty);

        return photoId;
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

    private sealed record WidgetDto(
        Guid? ActiveGroupId,
        string? ActiveGroupName,
        string? ActiveGroupAvatarUrl,
        Guid? LatestPhotoId,
        string? LatestPhotoUrl,
        DateTime? LatestPhotoCreatedAt,
        int NewReactionsCount);
}