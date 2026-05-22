using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Reports;

public sealed class ReportsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ReportsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Create_Report_For_Photo()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"owner_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("reports-group");
        await InviteAndAccept(groupId, reporter.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var response = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = photoId,
            reason = 1,
            description = "spam report"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reportId = await response.Content.ReadFromJsonAsync<Guid>();
        reportId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task User_Cannot_Create_Report_For_Missing_Target()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var response = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = Guid.NewGuid(),
            reason = 1,
            description = "missing target"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_Cannot_Create_Report_With_Invalid_TargetType()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var response = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 999,
            targetId = Guid.NewGuid(),
            reason = 1,
            description = "invalid target type"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_Cannot_Create_Report_With_Invalid_Reason()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var response = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = Guid.NewGuid(),
            reason = 999,
            description = "invalid reason"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_Cannot_Create_Duplicate_Pending_Report_For_Same_Target()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"owner_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("reports-group");
        await InviteAndAccept(groupId, reporter.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var first = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = photoId,
            reason = 1,
            description = "first"
        }));

        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = photoId,
            reason = 1,
            description = "second"
        }));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_Cannot_Report_Himself()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var response = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 3,
            targetId = reporter.id,
            reason = 7,
            description = "self report"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_Can_List_My_Reports()
    {
        var reporter = await Register($"reporter_{Guid.NewGuid():N}@test.com", $"reporter_{Guid.NewGuid():N}");
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"owner_{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("reports-group");
        await InviteAndAccept(groupId, reporter.email, ownerToken);

        var photoId = await PublishPhoto(groupId, owner.id, ownerToken);

        var reporterToken = await Login(reporter.email);
        SetAuth(_client, reporterToken);

        var create = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = photoId,
            reason = 1,
            description = "my report"
        }));

        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync("/api/reports/my");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<ReportDto>>();
        items.Should().NotBeNull();
        items!.Should().ContainSingle();

        var item = items[0];
        item.TargetType.Should().Be(1);
        item.TargetId.Should().Be(photoId);
        item.Reason.Should().Be(1);
        item.Description.Should().Be("my report");
    }

    [Fact]
    public async Task Reports_Endpoints_Require_Authorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var create = await _client.PostAsync("/api/reports", Json(new
        {
            targetType = 1,
            targetId = Guid.NewGuid(),
            reason = 1,
            description = "x"
        }));

        var my = await _client.GetAsync("/api/reports/my");
        var all = await _client.GetAsync("/api/reports");

        create.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        my.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        all.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private sealed record ReportDto(
        Guid Id,
        Guid ReporterUserId,
        int TargetType,
        Guid TargetId,
        int Reason,
        string? Description,
        int Status,
        Guid? ReviewedByUserId,
        DateTime? ReviewedAt,
        DateTime CreatedAt);
}