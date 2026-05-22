using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Media;

public sealed class PhotoDiscussionFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PhotoDiscussionFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Member_Can_Add_Comment_Reply_And_Reaction_To_Group_Photo()
    {
        var owner = await Register($"owner_media_{Guid.NewGuid():N}@test.com", $"ownermedia_{Guid.NewGuid():N}");
        var member = await Register($"member_media_{Guid.NewGuid():N}@test.com", $"membermedia_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("media-discussion-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var commentResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "first comment" }));

        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commentId = await commentResponse.Content.ReadFromJsonAsync<Guid>();
        commentId.Should().NotBe(Guid.Empty);

        var replyResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments/reply",
            Json(new
            {
                parentCommentId = commentId,
                text = "reply comment"
            }));

        replyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var replyId = await replyResponse.Content.ReadFromJsonAsync<Guid>();
        replyId.Should().NotBe(Guid.Empty);

        var reactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new { type = 1 }));

        reactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var commentsResponse = await _client.GetAsync($"/api/photos/{photoId}/comments");
        commentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var comments = await commentsResponse.Content.ReadFromJsonAsync<List<CommentDto>>();
        comments.Should().NotBeNull();
        comments!.Should().Contain(x => x.Id == commentId && x.Text == "first comment" && x.ParentCommentId == null);
        comments.Should().Contain(x => x.Id == replyId && x.Text == "reply comment" && x.ParentCommentId == commentId);

        var reactionsResponse = await _client.GetAsync($"/api/photos/{photoId}/reactions");
        reactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactions = await reactionsResponse.Content.ReadFromJsonAsync<ReactionsSummaryDto>();
        reactions.Should().NotBeNull();
        reactions!.PhotoId.Should().Be(photoId);
        ((int)reactions.MyReaction).Should().Be(1);
        reactions.Counts.Should().ContainSingle(x => (int)x.Type == 1 && x.Count == 1);
    }

    [Fact]
    public async Task User_Can_Update_Reaction_On_Same_Photo()
    {
        var owner = await Register($"owner_react_{Guid.NewGuid():N}@test.com", $"ownerreact_{Guid.NewGuid():N}");
        var member = await Register($"member_react_{Guid.NewGuid():N}@test.com", $"memberreact_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("reaction-update-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var addReactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new { type = 1 }));

        addReactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updateReactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new { type = 3 }));

        updateReactionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reactionsResponse = await _client.GetAsync($"/api/photos/{photoId}/reactions");
        reactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactions = await reactionsResponse.Content.ReadFromJsonAsync<ReactionsSummaryDto>();
        reactions.Should().NotBeNull();
        reactions!.PhotoId.Should().Be(photoId);
        ((int)reactions.MyReaction).Should().Be(3);
        reactions.Counts.Should().ContainSingle(x => (int)x.Type == 3 && x.Count == 1);
    }

    [Fact]
    public async Task User_Can_Edit_And_Delete_Own_Comment()
    {
        var owner = await Register($"owner_comment_{Guid.NewGuid():N}@test.com", $"ownercomment_{Guid.NewGuid():N}");
        var member = await Register($"member_comment_{Guid.NewGuid():N}@test.com", $"membercomment_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("comment-edit-delete-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var createResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "initial text" }));

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commentId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        commentId.Should().NotBe(Guid.Empty);

        var updateResponse = await _client.PatchAsync(
            $"/api/comments/{commentId}",
            Json(new { text = "updated text" }));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var editedId = await updateResponse.Content.ReadFromJsonAsync<Guid>();
        editedId.Should().Be(commentId);

        var commentsAfterUpdateResponse = await _client.GetAsync($"/api/photos/{photoId}/comments");
        commentsAfterUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commentsAfterUpdate = await commentsAfterUpdateResponse.Content.ReadFromJsonAsync<List<CommentDto>>();
        commentsAfterUpdate.Should().NotBeNull();
        commentsAfterUpdate!.Should().ContainSingle(x => x.Id == commentId && x.Text == "updated text");

        var deleteResponse = await _client.DeleteAsync($"/api/comments/{commentId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var commentsAfterDeleteResponse = await _client.GetAsync($"/api/photos/{photoId}/comments");
        commentsAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commentsAfterDelete = await commentsAfterDeleteResponse.Content.ReadFromJsonAsync<List<CommentDto>>();
        commentsAfterDelete.Should().NotBeNull();
        commentsAfterDelete!.Should().NotContain(x => x.Id == commentId);
    }

    [Fact]
    public async Task ExMember_Cannot_Access_Photo_Discussion_After_Leaving_Group()
    {
        var owner = await Register($"owner_exmember_{Guid.NewGuid():N}@test.com", $"ownerexm_{Guid.NewGuid():N}");
        var member = await Register($"member_exmember_{Guid.NewGuid():N}@test.com", $"memberexm_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("discussion-access-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var leaveResponse = await _client.PostAsync($"/api/groups/{groupId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var commentsResponse = await _client.GetAsync($"/api/photos/{photoId}/comments");
        var reactionsResponse = await _client.GetAsync($"/api/photos/{photoId}/reactions");

        commentsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        reactionsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var addCommentResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "should fail" }));

        addCommentResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var addReactionResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/reactions",
            Json(new { type = 2 }));

        addReactionResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private sealed record CommentDto(
        Guid Id,
        Guid PhotoId,
        Guid UserId,
        Guid? ParentCommentId,
        string Text,
        DateTime CreatedAt,
        DateTime? EditedAt
    );

    private sealed record ReactionCountDto(int Type, int Count);

    private sealed record ReactionsSummaryDto(
        Guid PhotoId,
        int MyReaction,
        IReadOnlyList<ReactionCountDto> Counts
    );
}