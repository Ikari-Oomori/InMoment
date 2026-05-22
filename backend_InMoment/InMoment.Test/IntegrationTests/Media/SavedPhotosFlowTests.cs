using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Media;

public sealed class SavedPhotosFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SavedPhotosFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Save_And_Unsave_Accessible_Photo()
    {
        var owner = await Register($"owner_saved_{Guid.NewGuid():N}@test.com", $"ownersaved_{Guid.NewGuid():N}");
        var member = await Register($"member_saved_{Guid.NewGuid():N}@test.com", $"membersaved_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("saved-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, "saved caption");

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var saveResponse = await _client.PostAsync($"/api/photos/{photoId}/save", null);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var savedResponse = await _client.GetAsync("/api/photos/saved");
        savedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var savedPhotos = await savedResponse.Content.ReadFromJsonAsync<SavedPhotosPageDto>();
        savedPhotos.Should().NotBeNull();
        savedPhotos!.Items.Should().Contain(x => x.PhotoId == photoId);

        var item = savedPhotos.Items.Single(x => x.PhotoId == photoId);
        item.GroupId.Should().Be(groupId);
        item.GroupName.Should().Be("saved-group");
        item.UploadedByUserId.Should().Be(owner.id);
        item.UploadedByUserName.Should().NotBeNullOrWhiteSpace();
        item.IsMine.Should().BeFalse();
        item.PhotoUrl.Should().NotBeNullOrWhiteSpace();
        item.Caption.Should().Be("saved caption");

        var unsaveResponse = await _client.DeleteAsync($"/api/photos/{photoId}/save");
        unsaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var savedAfterUnsaveResponse = await _client.GetAsync("/api/photos/saved");
        savedAfterUnsaveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var savedAfterUnsave = await savedAfterUnsaveResponse.Content.ReadFromJsonAsync<SavedPhotosPageDto>();
        savedAfterUnsave.Should().NotBeNull();
        savedAfterUnsave!.Items.Should().NotContain(x => x.PhotoId == photoId);
    }

    [Fact]
    public async Task User_Cannot_Save_Photo_From_Group_That_Is_Not_Accessible()
    {
        var owner = await Register($"owner_nosave_{Guid.NewGuid():N}@test.com", $"ownernosave_{Guid.NewGuid():N}");
        var outsider = await Register($"outsider_nosave_{Guid.NewGuid():N}@test.com", $"outsidernosave_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("private-save-group");
        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, null);

        var outsiderToken = await Login(outsider.email);
        SetAuth(_client, outsiderToken);

        var saveResponse = await _client.PostAsync($"/api/photos/{photoId}/save", null);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var savedResponse = await _client.GetAsync("/api/photos/saved");
        savedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var savedPhotos = await savedResponse.Content.ReadFromJsonAsync<SavedPhotosPageDto>();
        savedPhotos.Should().NotBeNull();
        savedPhotos!.Items.Should().NotContain(x => x.PhotoId == photoId);
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

    private async Task<Guid> PublishPhoto(Guid groupId, Guid uploaderId, string token, string? caption)
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

    private sealed record SavedPhotoItemDto(
        Guid PhotoId,
        Guid GroupId,
        string GroupName,
        string? GroupAvatarUrl,
        Guid UploadedByUserId,
        string UploadedByUserName,
        string? UploadedByUserProfilePhotoUrl,
        bool IsMine,
        string PhotoUrl,
        string ContentType,
        long SizeBytes,
        string? Caption,
        DateTime PhotoCreatedAt,
        DateTime SavedAt
    );

    private sealed record SavedPhotosPageDto(
        IReadOnlyList<SavedPhotoItemDto> Items,
        string? NextCursor
    );
}