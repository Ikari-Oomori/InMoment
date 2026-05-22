using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.Domain.Contacts;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Contacts;

public sealed class ContactInviteFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ContactInviteFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task User_Can_Send_List_And_Cancel_Email_Invite()
    {
        var email = $"contact_inviter_{Guid.NewGuid():N}@test.com";
        var userName = $"contactinviter_{Guid.NewGuid():N}";

        await Register(email, userName);
        var token = await Login(email);
        SetAuth(_client, token);

        var sendResponse = await _client.PostAsync("/api/contacts/invites", Json(new
        {
            email = "friend@example.com",
            displayName = "Friend"
        }));

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invite = await sendResponse.Content.ReadFromJsonAsync<ContactInviteDto>();
        invite.Should().NotBeNull();
        invite!.Channel.Should().Be(ContactInviteChannel.Email);
        invite.Status.Should().Be(ContactInviteStatus.Pending);

        var listResponse = await _client.GetAsync("/api/contacts/invites");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invites = await listResponse.Content.ReadFromJsonAsync<List<ContactInviteDto>>();
        invites.Should().NotBeNull();
        invites!.Should().ContainSingle(x => x.Id == invite.Id);

        var cancelResponse = await _client.PostAsync($"/api/contacts/invites/{invite.Id}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterCancelResponse = await _client.GetAsync("/api/contacts/invites");
        afterCancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterCancel = await afterCancelResponse.Content.ReadFromJsonAsync<List<ContactInviteDto>>();
        afterCancel.Should().NotBeNull();
        afterCancel!.Single(x => x.Id == invite.Id).Status.Should().Be(ContactInviteStatus.Cancelled);
    }

    [Fact]
    public async Task User_Cannot_Send_Invite_To_AlreadyRegistered_Email()
    {
        var inviterEmail = $"contact_inviter2_{Guid.NewGuid():N}@test.com";
        var inviterUserName = $"contactinviter2_{Guid.NewGuid():N}";
        await Register(inviterEmail, inviterUserName);

        var registeredEmail = $"registered_{Guid.NewGuid():N}@test.com";
        var registeredUserName = $"registered_{Guid.NewGuid():N}";
        await Register(registeredEmail, registeredUserName);

        var token = await Login(inviterEmail);
        SetAuth(_client, token);

        var sendResponse = await _client.PostAsync("/api/contacts/invites", Json(new
        {
            email = registeredEmail,
            displayName = "Already There"
        }));

        sendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private sealed record ContactInviteDto(
        Guid Id,
        ContactInviteChannel Channel,
        string? Email,
        string? PhoneNumber,
        string? DisplayName,
        string InviteToken,
        ContactInviteStatus Status,
        DateTime CreatedAtUtc,
        DateTime? CancelledAtUtc);
}