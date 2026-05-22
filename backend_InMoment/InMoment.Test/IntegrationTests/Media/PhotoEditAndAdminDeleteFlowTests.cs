using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Media;

public sealed class PhotoEditAndAdminDeleteFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PhotoEditAndAdminDeleteFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Author_Can_Edit_Photo_Caption()
    {
        var owner = await Register(
            $"owner_edit_{Guid.NewGuid():N}@test.com",
            $"owneredit_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);

        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("photo-edit-group");
        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, "old caption");

        var editResponse = await _client.PatchAsync(
            $"/api/groups/{groupId}/photos/{photoId}",
            Json(new { caption = "new caption" }));

        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailsResponse = await _client.GetAsync($"/api/photos/{photoId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await detailsResponse.Content.ReadFromJsonAsync<PhotoDetailsDto>();
        dto.Should().NotBeNull();
        dto!.Caption.Should().Be("new caption");
        dto.IsMine.Should().BeTrue();
        dto.CanEdit.Should().BeTrue();
        dto.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_Can_Delete_Other_Users_Photo_But_Cannot_Edit_It()
    {
        var owner = await Register(
            $"owner_admin_delete_{Guid.NewGuid():N}@test.com",
            $"owneradmindel_{Guid.NewGuid():N}");

        var admin = await Register(
            $"admin_delete_{Guid.NewGuid():N}@test.com",
            $"admindel_{Guid.NewGuid():N}");

        var member = await Register(
            $"member_delete_{Guid.NewGuid():N}@test.com",
            $"memberdel_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        var adminToken = await Login(admin.email);
        var memberToken = await Login(member.email);

        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("photo-admin-delete-group");

        await InviteAndAccept(groupId, admin.email, ownerToken);
        await InviteAndAccept(groupId, member.email, ownerToken);

        var promoteResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/members/{admin.id}/make-admin",
            null);

        promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var photoId = await PublishPhoto(groupId, member.id, memberToken, "member caption");

        SetAuth(_client, adminToken);

        var detailsBeforeDelete = await _client.GetAsync($"/api/photos/{photoId}");
        detailsBeforeDelete.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await detailsBeforeDelete.Content.ReadFromJsonAsync<PhotoDetailsDto>();
        dto.Should().NotBeNull();
        dto!.IsMine.Should().BeFalse();
        dto.CanEdit.Should().BeFalse();
        dto.CanDelete.Should().BeTrue();

        var editResponse = await _client.PatchAsync(
            $"/api/groups/{groupId}/photos/{photoId}",
            Json(new { caption = "admin edited" }));

        editResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/groups/{groupId}/photos/{photoId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailsAfterDelete = await _client.GetAsync($"/api/photos/{photoId}");
        detailsAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task<Guid> PublishPhoto(
        Guid groupId,
        Guid uploaderId,
        string token,
        string? caption)
    {
        SetAuth(_client, token);

        var storageKey = $"groups/{groupId}/photos/{uploaderId}/{Guid.NewGuid():N}.jpg";

        var response = await _client.PostAsync(
            $"/api/groups/{groupId}/photos",
            Json(new
            {
                storageKey,
                contentType = "image/jpeg",
                sizeBytes = 1024,
                caption
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payloadText = await response.Content.ReadAsStringAsync();

        if (Guid.TryParse(payloadText.Trim('"', ' ', '\n', '\r', '\t'), out var directGuid))
            return directGuid;

        var dto = await response.Content.ReadFromJsonAsync<PublishPhotoResponse>();
        dto.Should().NotBeNull();
        return dto!.PhotoId;
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

    private sealed record PublishPhotoResponse(Guid PhotoId);

    private sealed record PhotoDetailsDto(
        Guid PhotoId,
        Guid GroupId,
        Guid AuthorId,
        string AuthorUserName,
        string AuthorFirstName,
        string AuthorLastName,
        string? AuthorProfilePhotoUrl,
        string Url,
        string ContentType,
        long SizeBytes,
        string? Caption,
        DateTime CreatedAt,
        bool IsMine,
        bool CanEdit,
        bool CanDelete);
}