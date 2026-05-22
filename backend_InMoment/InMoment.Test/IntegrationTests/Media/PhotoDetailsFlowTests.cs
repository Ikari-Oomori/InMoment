using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.Domain.Media;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Media;

public sealed class PhotoDetailsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PhotoDetailsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Member_Can_Get_Photo_Details_With_Caption_Reactions_And_CommentsCount()
    {
        var owner = await Register(
            $"owner_details_{Guid.NewGuid():N}@test.com",
            $"ownerdetails_{Guid.NewGuid():N}");

        var member = await Register(
            $"member_details_{Guid.NewGuid():N}@test.com",
            $"memberdetails_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        var memberToken = await Login(member.email);

        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("photo-details-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(
            groupId,
            owner.id,
            ownerToken,
            "hello from caption");

        SetAuth(_client, memberToken);

        var commentResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "nice one" }));

        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new { type = ReactionType.Heart }));

        reactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailsResponse = await _client.GetAsync($"/api/photos/{photoId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await detailsResponse.Content.ReadFromJsonAsync<PhotoDetailsDto>();
        dto.Should().NotBeNull();

        dto!.PhotoId.Should().Be(photoId);
        dto.GroupId.Should().Be(groupId);
        dto.AuthorId.Should().Be(owner.id);
        dto.AuthorUserName.Should().NotBeNullOrWhiteSpace();
        dto.Url.Should().NotBeNullOrWhiteSpace();
        dto.ContentType.Should().Be("image/jpeg");
        dto.Caption.Should().Be("hello from caption");
        dto.IsMine.Should().BeFalse();
        dto.CanEdit.Should().BeFalse();
        dto.CanDelete.Should().BeFalse();
        dto.MyReaction.Should().Be(ReactionType.Heart);
        dto.CommentsCount.Should().Be(1);
        dto.Reactions.Should().Contain(x => x.Type == ReactionType.Heart && x.Count == 1);
    }

    [Fact]
    public async Task NonMember_Cannot_Get_Photo_Details()
    {
        var owner = await Register(
            $"owner_details2_{Guid.NewGuid():N}@test.com",
            $"ownerdetails2_{Guid.NewGuid():N}");

        var outsider = await Register(
            $"outsider_details_{Guid.NewGuid():N}@test.com",
            $"outsiderdetails_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        var outsiderToken = await Login(outsider.email);

        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("photo-details-forbidden-group");
        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, "secret caption");

        SetAuth(_client, outsiderToken);

        var detailsResponse = await _client.GetAsync($"/api/photos/{photoId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deleted_Photo_Should_Not_Be_Returned_From_Details()
    {
        var owner = await Register(
            $"owner_details3_{Guid.NewGuid():N}@test.com",
            $"ownerdetails3_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);

        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("photo-details-delete-group");
        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, "to be deleted");

        var deleteResponse = await _client.DeleteAsync($"/api/groups/{groupId}/photos/{photoId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailsResponse = await _client.GetAsync($"/api/photos/{photoId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        bool CanDelete,
        ReactionType MyReaction,
        IReadOnlyList<PhotoReactionCountDto> Reactions,
        int CommentsCount);

    private sealed record PhotoReactionCountDto(
        ReactionType Type,
        int Count);
}