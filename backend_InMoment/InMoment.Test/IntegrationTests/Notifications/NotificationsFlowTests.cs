using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Notifications;

public sealed class NotificationsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NotificationsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_List_Notifications_And_Mark_Them_Read()
    {
        var owner = await Register($"owner_notif_{Guid.NewGuid():N}@test.com", $"ownernotif_{Guid.NewGuid():N}");
        var member = await Register($"member_notif_{Guid.NewGuid():N}@test.com", $"membernotif_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("notifications-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken, "notification caption");

        var memberToken = await Login(member.email);
        SetAuth(_client, memberToken);

        var addCommentResponse = await _client.PostAsync(
            $"/api/photos/{photoId}/comments",
            Json(new { text = "notification trigger comment" }));

        addCommentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuth(_client, ownerToken);

        var ownerUnreadResponse = await _client.GetAsync("/api/notifications/unread-count");
        ownerUnreadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unreadBefore = await ownerUnreadResponse.Content.ReadFromJsonAsync<UnreadCountDto>();
        unreadBefore.Should().NotBeNull();
        unreadBefore!.Count.Should().BeGreaterThan(0);

        var listResponse = await _client.GetAsync("/api/notifications");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await listResponse.Content.ReadFromJsonAsync<NotificationsPageDto>();
        page.Should().NotBeNull();
        page!.Items.Should().NotBeEmpty();
        page.UnreadCount.Should().BeGreaterThan(0);

        var firstItem = page.Items[0];
        firstItem.ActorDisplayName.Should().NotBeNullOrWhiteSpace();
        firstItem.ActorUserName.Should().NotBeNullOrWhiteSpace();
        firstItem.GroupId.Should().Be(groupId);
        firstItem.GroupName.Should().Be("notifications-group");
        firstItem.PhotoId.Should().Be(photoId);
        firstItem.PhotoUrl.Should().NotBeNullOrWhiteSpace();
        firstItem.ThumbnailUrl.Should().NotBeNullOrWhiteSpace();
        firstItem.PhotoCaption.Should().Be("notification caption");
        firstItem.PreviewText.Should().NotBeNullOrWhiteSpace();

        var firstNotificationId = firstItem.Id;

        var markReadResponse = await _client.PostAsync(
            $"/api/notifications/{firstNotificationId}/read",
            null);

        markReadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var markAllReadResponse = await _client.PostAsync("/api/notifications/read-all", null);
        markAllReadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ownerUnreadAfterResponse = await _client.GetAsync("/api/notifications/unread-count");
        ownerUnreadAfterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unreadAfter = await ownerUnreadAfterResponse.Content.ReadFromJsonAsync<UnreadCountDto>();
        unreadAfter.Should().NotBeNull();
        unreadAfter!.Count.Should().Be(0);
    }

    [Fact]
    public async Task Notifications_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var listResponse = await _client.GetAsync("/api/notifications");
        var unreadResponse = await _client.GetAsync("/api/notifications/unread-count");
        var markAllResponse = await _client.PostAsync("/api/notifications/read-all", null);

        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unreadResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        markAllResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private sealed record NotificationItemDto(
        Guid Id,
        int Type,
        Guid? ActorUserId,
        string? ActorDisplayName,
        string? ActorUserName,
        string? ActorProfilePhotoUrl,
        Guid? GroupId,
        string? GroupName,
        string? GroupAvatarUrl,
        Guid? PhotoId,
        string? PhotoUrl,
        string? ThumbnailUrl,
        string? PhotoCaption,
        Guid? CommentId,
        Guid? InvitationId,
        bool IsRead,
        int AggregationCount,
        string PreviewText,
        int TargetType,
        Guid? TargetId,
        string? TargetRoute,
        bool IsClickable,
        string CreatedAtHumanized,
        DateTime CreatedAt,
        DateTime? ReadAt);

    private sealed record NotificationsPageDto(
        IReadOnlyList<NotificationItemDto> Items,
        string? NextCursor,
        int UnreadCount);

    private sealed record UnreadCountDto(int Count);
}