using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InMoment.IntegrationTests.Memories;

public sealed class MemoriesFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MemoriesFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Group_Memories_Endpoints_Work_After_Photo_Publish()
    {
        // Arrange
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var member = await Register($"member_{Guid.NewGuid():N}@test.com", $"Member{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("memories-group");
        await InviteAndAccept(groupId, member.email, ownerToken);

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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Group stats
        var statsResponse = await _client.GetAsync($"/api/groups/{groupId}/memories/stats");
        statsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statsJson = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();
        GetIntProperty(statsJson, "totalPhotos").Should().BeGreaterThanOrEqualTo(1);
        GetIntProperty(statsJson, "activeDays").Should().BeGreaterThanOrEqualTo(1);

        // Group calendar
        var calendarResponse = await _client.GetAsync(
            $"/api/groups/{groupId}/calendar?year={today.Year}&month={today.Month}");
        calendarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var calendarJson = await calendarResponse.Content.ReadFromJsonAsync<JsonElement>();
        var postingDays = GetArrayProperty(calendarJson, "postingDays", "daysWithPhotos", "days");
        postingDays.GetArrayLength().Should().BeGreaterThan(0);

        // Group memories by date
        var memoriesResponse = await _client.GetAsync(
            $"/api/groups/{groupId}/memories?date={today:yyyy-MM-dd}");
        memoriesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var memoriesJson = await memoriesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = GetItemsArray(memoriesJson);
        items.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Personal_Memories_Endpoints_Work_After_User_Publishes_Photo()
    {
        // Arrange
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("personal-memories-group");

        var storageKey = $"groups/{groupId}/photos/{owner.id}/{Guid.NewGuid():N}.jpg";

        var publishResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/photos",
            Json(new
            {
                storageKey,
                contentType = "image/jpeg",
                sizeBytes = 1024
            }));

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Personal stats
        var statsResponse = await _client.GetAsync("/api/memories/personal/stats");
        statsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statsJson = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();
        GetIntProperty(statsJson, "totalPhotos").Should().BeGreaterThanOrEqualTo(1);
        GetIntProperty(statsJson, "activeDays").Should().BeGreaterThanOrEqualTo(1);

        // Personal calendar
        var calendarResponse = await _client.GetAsync(
            $"/api/memories/personal/calendar?year={today.Year}&month={today.Month}");
        calendarResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var calendarJson = await calendarResponse.Content.ReadFromJsonAsync<JsonElement>();
        var postingDays = GetArrayProperty(calendarJson, "postingDays", "daysWithPhotos", "days");
        postingDays.GetArrayLength().Should().BeGreaterThan(0);

        // Personal memories by date
        var memoriesResponse = await _client.GetAsync(
            $"/api/memories/personal?date={today:yyyy-MM-dd}");
        memoriesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var memoriesJson = await memoriesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = GetItemsArray(memoriesJson);
        items.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NonMember_Cannot_Read_Group_Memories()
    {
        // Arrange
        var owner = await Register($"owner_{Guid.NewGuid():N}@test.com", $"Owner{Guid.NewGuid():N}");
        var outsider = await Register($"outsider_{Guid.NewGuid():N}@test.com", $"Outsider{Guid.NewGuid():N}");

        var ownerToken = await Login(owner.email);
        SetAuth(_client, ownerToken);

        var groupId = await CreateGroup("forbidden-memories-group");

        var storageKey = $"groups/{groupId}/photos/{owner.id}/{Guid.NewGuid():N}.jpg";
        var publishResponse = await _client.PostAsync(
            $"/api/groups/{groupId}/photos",
            Json(new
            {
                storageKey,
                contentType = "image/jpeg",
                sizeBytes = 1024
            }));

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var outsiderToken = await Login(outsider.email);
        SetAuth(_client, outsiderToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var statsResponse = await _client.GetAsync($"/api/groups/{groupId}/memories/stats");
        statsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var calendarResponse = await _client.GetAsync(
            $"/api/groups/{groupId}/calendar?year={today.Year}&month={today.Month}");
        calendarResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var memoriesResponse = await _client.GetAsync(
            $"/api/groups/{groupId}/memories?date={today:yyyy-MM-dd}");
        memoriesResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static int GetIntProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Number)
                return value.GetInt32();
        }

        throw new Xunit.Sdk.XunitException(
            $"None of the integer properties [{string.Join(", ", names)}] were found.");
    }

    private static JsonElement GetArrayProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Array)
                return value;
        }

        throw new Xunit.Sdk.XunitException(
            $"None of the array properties [{string.Join(", ", names)}] were found.");
    }

    private static JsonElement GetItemsArray(JsonElement root)
    {
        if (TryGetProperty(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
            return items;

        if (TryGetProperty(root, "photos", out var photos) && photos.ValueKind == JsonValueKind.Array)
            return photos;

        if (TryGetProperty(root, "memories", out var memories) && memories.ValueKind == JsonValueKind.Array)
            return memories;

        throw new Xunit.Sdk.XunitException("Response does not contain an items array.");
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;

        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value))
            return true;

        return false;
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