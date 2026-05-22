using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InMoment.IntegrationTests.Media;

public sealed class PhotoCommentReactionFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PhotoCommentReactionFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Member_Can_Publish_Comment_React_And_Delete_Photo_Flow_Works()
    {
        // Arrange
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var member = await Register($"member_{Guid.NewGuid():N}@test.com", $"Member{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("media-flow-group");

        await InviteAndAccept(groupId, member.email, ownerToken);

        // Member publishes photo
        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var storageKey = $"groups/{groupId}/photos/{member.id}/{Guid.NewGuid():N}.jpg";

        var publishResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/photos",
            Json(new
            {
                storageKey,
                contentType = "image/jpeg",
                sizeBytes = 1024
            }));

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var photoId = await publishResponse.Content.ReadFromJsonAsync<Guid>();
        photoId.Should().NotBeEmpty();

        // Feed should contain 1 photo
        var feedAfterPublish = await _client.GetAsync($"/api/groups/{groupId}/feed");
        feedAfterPublish.StatusCode.Should().Be(HttpStatusCode.OK);

        var feedAfterPublishJson = await feedAfterPublish.Content.ReadFromJsonAsync<JsonElement>();
        feedAfterPublishJson.ValueKind.Should().Be(JsonValueKind.Array);
        feedAfterPublishJson.GetArrayLength().Should().Be(1);

        // Owner comments on photo
        ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var commentResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new
            {
                text = "nice photo"
            }));

        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commentId = await commentResponse.Content.ReadFromJsonAsync<Guid>();
        commentId.Should().NotBeEmpty();

        // Comments should contain 1 comment
        var commentsResponse = await _client.GetAsync($"/api/photos/{photoId}/comments");
        commentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commentsJson = await commentsResponse.Content.ReadFromJsonAsync<JsonElement>();
        commentsJson.ValueKind.Should().Be(JsonValueKind.Array);
        commentsJson.GetArrayLength().Should().Be(1);

        // Owner reacts to photo
        var reactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new
            {
                type = 1 // Heart
            }));

        reactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Owner removes reaction
        var removeReactionResponse = await _client.DeleteAsync(
            $"/api/photos/{photoId}/reactions");

        removeReactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Member deletes photo
        memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var deletePhotoResponse = await _client.DeleteAsync(
            $"/api/groups/{groupId}/photos/{photoId}");

        deletePhotoResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Feed should become empty
        var feedAfterDelete = await _client.GetAsync($"/api/groups/{groupId}/feed");
        feedAfterDelete.StatusCode.Should().Be(HttpStatusCode.OK);

        var feedAfterDeleteJson = await feedAfterDelete.Content.ReadFromJsonAsync<JsonElement>();
        feedAfterDeleteJson.ValueKind.Should().Be(JsonValueKind.Array);
        feedAfterDeleteJson.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NonMember_Cannot_Publish_Photo_To_Group()
    {
        // Arrange
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var outsider = await Register($"outsider_{Guid.NewGuid():N}@test.com", $"Outsider{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("publish-forbidden-group");

        var outsiderToken = await Login(outsider.email);
        SetAuth(_client, outsiderToken);

        var storageKey = $"groups/{groupId}/photos/{outsider.id}/{Guid.NewGuid():N}.jpg";

        // Act
        var publishResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/photos",
            Json(new
            {
                storageKey,
                contentType = "image/jpeg",
                sizeBytes = 1024
            }));

        // Assert
        publishResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
}