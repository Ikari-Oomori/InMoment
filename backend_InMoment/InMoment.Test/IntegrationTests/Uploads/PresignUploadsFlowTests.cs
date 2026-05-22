using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Uploads;

public sealed class PresignUploadsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PresignUploadsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProfilePhotoPresign_Returns_Urls_For_Authorized_User()
    {
        var user = await Register(
            $"profile_presign_{Guid.NewGuid():N}@test.com",
            $"profilepresign_{Guid.NewGuid():N}");

        var token = await Login(user.email);
        SetAuth(_client, token);

        var response = await _client.PostAsync(
            "/api/uploads/profile-photo/presign",
            Json(new
            {
                contentType = "image/jpeg"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<PresignDto>();
        dto.Should().NotBeNull();
        dto!.UploadUrl.Should().NotBeNullOrWhiteSpace();
        dto.StorageKey.Should().Contain($"users/{user.id}/profile-photo/");
        dto.FileUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProfilePhotoPresign_Rejects_Unsupported_ContentType()
    {
        var user = await Register(
            $"profile_bad_{Guid.NewGuid():N}@test.com",
            $"profilebad_{Guid.NewGuid():N}");

        var token = await Login(user.email);
        SetAuth(_client, token);

        var response = await _client.PostAsync(
            "/api/uploads/profile-photo/presign",
            Json(new
            {
                contentType = "application/pdf"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GroupAvatarPresign_Works_For_GroupOwner()
    {
        var owner = await Register(
            $"avatar_owner_{Guid.NewGuid():N}@test.com",
            $"avatarowner_{Guid.NewGuid():N}");

        var token = await Login(owner.email);
        SetAuth(_client, token);

        var groupId = await CreateGroup("Avatar Owners Group");

        var response = await _client.PostAsync(
            "/api/uploads/group-avatar/presign",
            Json(new
            {
                groupId,
                contentType = "image/png"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<PresignDto>();
        dto.Should().NotBeNull();
        dto!.StorageKey.Should().Contain($"groups/{groupId}/avatar/");
        dto.StorageKey.Should().EndWith(".png");
    }

    [Fact]
    public async Task GroupAvatarPresign_Is_Forbidden_For_NonOwner()
    {
        var owner = await Register(
            $"avatar_owner2_{Guid.NewGuid():N}@test.com",
            $"avatarowner2_{Guid.NewGuid():N}");

        var member = await Register(
            $"avatar_member2_{Guid.NewGuid():N}@test.com",
            $"avatarmember2_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Avatar Restricted Group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var response = await _client.PostAsync(
            "/api/uploads/group-avatar/presign",
            Json(new
            {
                groupId,
                contentType = "image/webp"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PhotoPresign_Works_For_GroupMember()
    {
        var owner = await Register(
            $"photo_owner_{Guid.NewGuid():N}@test.com",
            $"photoowner_{Guid.NewGuid():N}");

        var member = await Register(
            $"photo_member_{Guid.NewGuid():N}@test.com",
            $"photomember_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Photo Upload Group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var response = await _client.PostAsync(
            "/api/uploads/photos/presign",
            Json(new
            {
                groupId,
                contentType = "image/heic"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<PresignDto>();
        dto.Should().NotBeNull();
        dto!.StorageKey.Should().Contain($"groups/{groupId}/photos/{member.id}/");
        dto.StorageKey.Should().EndWith(".heic");
    }

    [Fact]
    public async Task PhotoPresign_Is_Forbidden_For_NonMember()
    {
        var owner = await Register(
            $"photo_owner2_{Guid.NewGuid():N}@test.com",
            $"photoowner2_{Guid.NewGuid():N}");

        var outsider = await Register(
            $"photo_outsider2_{Guid.NewGuid():N}@test.com",
            $"photooutsider2_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("Private Photo Group");

        var outsiderToken = await Login(outsider.email);
        SetAuth(_client, outsiderToken);

        var response = await _client.PostAsync(
            "/api/uploads/photos/presign",
            Json(new
            {
                groupId,
                contentType = "image/jpeg"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadPresign_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var profile = await _client.PostAsync("/api/uploads/profile-photo/presign", Json(new { contentType = "image/jpeg" }));
        var photo = await _client.PostAsync("/api/uploads/photos/presign", Json(new { groupId = Guid.NewGuid(), contentType = "image/jpeg" }));
        var avatar = await _client.PostAsync("/api/uploads/group-avatar/presign", Json(new { groupId = Guid.NewGuid(), contentType = "image/jpeg" }));

        profile.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        photo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        avatar.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private sealed record PresignDto(
        string UploadUrl,
        string StorageKey,
        string FileUrl,
        DateTimeOffset ExpiresAt);
}