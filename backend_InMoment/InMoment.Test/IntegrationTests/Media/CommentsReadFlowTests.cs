using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Media;

public sealed class CommentsReadFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CommentsReadFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Member_Can_Get_Comments_With_Author_And_Reply_Context()
    {
        var owner = await Register(
            $"ocr_{Guid.NewGuid():N}@test.com",
            $"ocr_{Guid.NewGuid():N}");

        var member = await Register(
            $"mcr_{Guid.NewGuid():N}@test.com",
            $"mcr_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        var memberToken = await Login(member.email);

        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("comments-read-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var rootResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "root text" }));

        rootResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rootId = await rootResponse.Content.ReadFromJsonAsync<Guid>();

        SetAuth(_client, memberToken);

        var replyResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments/reply",
            Json(new
            {
                parentCommentId = rootId,
                text = "reply text"
            }));

        replyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await _client.GetAsync($"/api/photos/{photoId}/comments");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<List<CommentDto>>();
        list.Should().NotBeNull();
        list!.Should().HaveCount(2);

        list[0].UserName.Should().NotBeNullOrWhiteSpace();
        list[0].FirstName.Should().NotBeNullOrWhiteSpace();

        list[1].ParentCommentId.Should().Be(rootId);
        list[1].ParentCommentUserName.Should().NotBeNullOrWhiteSpace();
        list[1].ParentCommentTextPreview.Should().Be("root text");
        list[1].IsMine.Should().BeTrue();

        var pagedResponse = await _client.GetAsync($"/api/photos/{photoId}/comments/paged?limit=20");
        pagedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var paged = await pagedResponse.Content.ReadFromJsonAsync<CommentsPageDto>();
        paged.Should().NotBeNull();
        paged.Items.Should().HaveCount(2);

        var replyDto = paged.Items.Single(x => x.ParentCommentId.HasValue);

        replyDto.ParentCommentId.Should().Be(rootId);
        replyDto.ParentCommentTextPreview.Should().Be("root text");
        replyDto.IsMine.Should().BeTrue();
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
        string UserName,
        string FirstName,
        string LastName,
        string? ProfilePhotoUrl,
        Guid? ParentCommentId,
        Guid? ParentCommentUserId,
        string? ParentCommentUserName,
        string? ParentCommentTextPreview,
        string Text,
        DateTime CreatedAt,
        DateTime? EditedAt,
        bool IsMine);

    private sealed record PagedCommentDto(
        Guid Id,
        Guid PhotoId,
        Guid UserId,
        string UserName,
        string FirstName,
        string LastName,
        string? ProfilePhotoUrl,
        Guid? ParentCommentId,
        Guid? ParentCommentUserId,
        string? ParentCommentUserName,
        string? ParentCommentTextPreview,
        string Text,
        DateTime CreatedAt,
        DateTime? EditedAt,
        bool IsMine);

    private sealed record CommentsPageDto(
        IReadOnlyList<PagedCommentDto> Items,
        string? NextCursor);
}