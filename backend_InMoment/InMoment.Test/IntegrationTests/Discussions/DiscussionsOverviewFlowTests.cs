using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Discussions;

public sealed class DiscussionsOverviewFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DiscussionsOverviewFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Group_Member_Can_Get_Discussions_Overview()
    {
        var owner = await Register($"owner_disc_{Guid.NewGuid():N}@test.com", $"ownerdisc_{Guid.NewGuid():N}");
        var member = await Register($"member_disc_{Guid.NewGuid():N}@test.com", $"memberdisc_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("discussions-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, "discussion caption");

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var addCommentResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "discussion comment" }));

        addCommentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var discussionsResponse = await _client.GetAsync($"/api/groups/{groupId}/discussions");
        discussionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await discussionsResponse.Content.ReadFromJsonAsync<List<DiscussionItemDto>>();
        items.Should().NotBeNull();
        items!.Should().ContainSingle(x => x.PhotoId == photoId);

        var item = items.Single(x => x.PhotoId == photoId);
        item.PhotoUrl.Should().NotBeNullOrWhiteSpace();
        item.PhotoAuthorUserId.Should().Be(owner.id);
        item.PhotoAuthorUserName.Should().NotBeNullOrWhiteSpace();
        item.PhotoCaption.Should().Be("discussion caption");
        item.CommentsCount.Should().Be(1);
        item.LatestCommentText.Should().Be("discussion comment");
        item.LatestCommentUserId.Should().Be(member.id);
        item.LatestCommentUserName.Should().NotBeNullOrWhiteSpace();
        item.LatestCommentCreatedAt.Should().NotBeNull();
        item.LastActivityAt.Should().Be(item.LatestCommentCreatedAt!.Value);
    }

    [Fact]
    public async Task NonMember_Cannot_Get_Discussions_Overview()
    {
        var owner = await Register($"owner_disc2_{Guid.NewGuid():N}@test.com", $"ownerdisc2_{Guid.NewGuid():N}");
        var outsider = await Register($"outsider_disc2_{Guid.NewGuid():N}@test.com", $"outsiderdisc2_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("private-discussions-group");
        await PublishPhoto(groupId, owner.id, ownerToken, null);

        var outsiderToken = await Login(outsider.email);
        SetAuth(_client, outsiderToken);

        var discussionsResponse = await _client.GetAsync($"/api/groups/{groupId}/discussions");
        discussionsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private sealed record DiscussionItemDto(
        Guid PhotoId,
        string PhotoUrl,
        DateTime PhotoCreatedAt,
        string? PhotoCaption,
        Guid PhotoAuthorUserId,
        string PhotoAuthorUserName,
        string? PhotoAuthorProfilePhotoUrl,
        int CommentsCount,
        string? LatestCommentText,
        Guid? LatestCommentUserId,
        string? LatestCommentUserName,
        string? LatestCommentUserProfilePhotoUrl,
        DateTime? LatestCommentCreatedAt,
        DateTime LastActivityAt);
}