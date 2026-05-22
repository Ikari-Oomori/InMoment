using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;
using InMoment.Domain.Notifications;

namespace InMoment.IntegrationTests.Notifications;

public sealed class NotificationSettingsFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NotificationSettingsFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Get_And_Update_Notification_Settings()
    {
        var email = $"notify_settings_{Guid.NewGuid():N}@test.com";
        var userName = $"notifyset_{Guid.NewGuid():N}";

        await Register(email, userName);
        var token = await Login(email);
        SetAuth(_client, token);

        var getResponse = await _client.GetAsync("/api/notifications/settings");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initial = await getResponse.Content.ReadFromJsonAsync<NotificationSettingsDto>();
        initial.Should().NotBeNull();
        initial!.PushEnabled.Should().BeTrue();

        var updateResponse = await _client.PutAsync("/api/notifications/settings", Json(new
        {
            pushEnabled = true,
            pushGroupInvitations = false,
            pushReactions = false,
            pushComments = true,
            pushReplies = false,
            pushMentions = true
        }));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<NotificationSettingsDto>();
        updated.Should().NotBeNull();
        updated!.PushGroupInvitations.Should().BeFalse();
        updated.PushReactions.Should().BeFalse();
        updated.PushComments.Should().BeTrue();
        updated.PushReplies.Should().BeFalse();
        updated.PushMentions.Should().BeTrue();
    }

    [Fact]
    public async Task User_Can_Register_List_And_Revoke_Device_Token()
    {
        var email = $"notify_device_{Guid.NewGuid():N}@test.com";
        var userName = $"notifydev_{Guid.NewGuid():N}";

        await Register(email, userName);
        var token = await Login(email);
        SetAuth(_client, token);

        var registerResponse = await _client.PostAsync("/api/notifications/devices", Json(new
        {
            token = $"device_token_{Guid.NewGuid():N}",
            platform = PushPlatform.Android,
            provider = PushProvider.Fcm,
            deviceName = "Pixel Test"
        }));

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await registerResponse.Content.ReadFromJsonAsync<DeviceTokenDto>();
        device.Should().NotBeNull();
        device!.IsActive.Should().BeTrue();

        var listResponse = await _client.GetAsync("/api/notifications/devices");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await listResponse.Content.ReadFromJsonAsync<List<DeviceTokenDto>>();
        devices.Should().NotBeNull();
        devices!.Should().ContainSingle(x => x.Id == device.Id && x.IsActive);

        var revokeResponse = await _client.DeleteAsync($"/api/notifications/devices/{device.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRevokeResponse = await _client.GetAsync("/api/notifications/devices");
        afterRevokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRevoke = await afterRevokeResponse.Content.ReadFromJsonAsync<List<DeviceTokenDto>>();
        afterRevoke.Should().NotBeNull();
        afterRevoke!.Should().ContainSingle(x => x.Id == device.Id && x.IsActive == false);
    }

    private async Task Register(string email, string userName)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName = "Anna",
            lastName = "Petrova",
            userName
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return auth!.accessToken;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record NotificationSettingsDto(
        bool PushEnabled,
        bool PushGroupInvitations,
        bool PushReactions,
        bool PushComments,
        bool PushReplies,
        bool PushMentions,
        DateTime? CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    private sealed record DeviceTokenDto(
        Guid Id,
        string Token,
        PushPlatform Platform,
        PushProvider Provider,
        string? DeviceName,
        bool IsActive,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime LastUsedAtUtc);
}