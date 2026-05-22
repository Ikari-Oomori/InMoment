using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Users;

public sealed class UserOnboardingFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserOnboardingFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Then_GetMe_ShouldReturn_DefaultOnboardingState()
    {
        var email = $"onboarding_{Guid.NewGuid():N}@test.com";
        var userName = $"onboarding_{Guid.NewGuid():N}";

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await response.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();

        me!.Onboarding.IsCompleted.Should().BeFalse();
        me.Onboarding.NeedsOnboarding.Should().BeTrue();
        me.Onboarding.HasCompletedContactsStep.Should().BeFalse();
        me.Onboarding.SkippedContactsImport.Should().BeFalse();
        me.Onboarding.CompletedAt.Should().BeNull();
        me.Onboarding.CanFinishOnboarding.Should().BeFalse();

        me.ProfileCompleteness.HasPhoneNumber.Should().BeFalse();
        me.ProfileCompleteness.HasProfilePhoto.Should().BeFalse();
        me.ProfileCompleteness.HasActiveGroup.Should().BeFalse();
    }

    [Fact]
    public async Task SkipContacts_Then_GetMe_ShouldExposeContactsStepState()
    {
        var email = $"skip_contacts_{Guid.NewGuid():N}@test.com";
        var userName = $"skipcontacts_{Guid.NewGuid():N}";

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var skipResponse = await _client.PostAsync("/api/users/me/onboarding/skip-contacts", null);
        skipResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await _client.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();

        me!.Onboarding.IsCompleted.Should().BeFalse();
        me.Onboarding.NeedsOnboarding.Should().BeTrue();
        me.Onboarding.HasCompletedContactsStep.Should().BeTrue();
        me.Onboarding.SkippedContactsImport.Should().BeTrue();
        me.Onboarding.CompletedAt.Should().BeNull();
        me.Onboarding.CanFinishOnboarding.Should().BeTrue();
    }

    [Fact]
    public async Task SkipContacts_Then_CompleteOnboarding_Then_GetMe_ShouldReturnCompletedState()
    {
        var email = $"complete_onboarding_{Guid.NewGuid():N}@test.com";
        var userName = $"completeonb_{Guid.NewGuid():N}";

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var skipResponse = await _client.PostAsync("/api/users/me/onboarding/skip-contacts", null);
        skipResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var completeResponse = await _client.PostAsync("/api/users/me/onboarding/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meResponse = await _client.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();

        me!.Onboarding.IsCompleted.Should().BeTrue();
        me.Onboarding.NeedsOnboarding.Should().BeFalse();
        me.Onboarding.HasCompletedContactsStep.Should().BeTrue();
        me.Onboarding.SkippedContactsImport.Should().BeTrue();
        me.Onboarding.CompletedAt.Should().NotBeNull();
        me.Onboarding.CanFinishOnboarding.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteOnboarding_WithoutContactsStep_ShouldReturnBadRequest()
    {
        var email = $"complete_without_contacts_{Guid.NewGuid():N}@test.com";
        var userName = $"completewc_{Guid.NewGuid():N}";

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var completeResponse = await _client.PostAsync("/api/users/me/onboarding/complete", null);

        completeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportContacts_Then_GetMe_ShouldMarkContactsStepCompletedWithoutSkip()
    {
        var email = $"import_contacts_{Guid.NewGuid():N}@test.com";
        var userName = $"importcontacts_{Guid.NewGuid():N}";

        await Register(email, userName, "Anna", "Petrova");
        var token = await Login(email);

        SetAuth(_client, token);

        var importResponse = await _client.PostAsync("/api/contacts/import", Json(new
        {
            contacts = new[]
            {
                new
                {
                    displayName = "Unknown Contact",
                    phones = Array.Empty<string>(),
                    emails = new[] { "nobody@example.com" }
                }
            }
        }));

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResponse = await _client.GetAsync("/api/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<MeDto>();
        me.Should().NotBeNull();

        me!.Onboarding.IsCompleted.Should().BeFalse();
        me.Onboarding.NeedsOnboarding.Should().BeTrue();
        me.Onboarding.HasCompletedContactsStep.Should().BeTrue();
        me.Onboarding.SkippedContactsImport.Should().BeFalse();
        me.Onboarding.CompletedAt.Should().BeNull();
        me.Onboarding.CanFinishOnboarding.Should().BeTrue();
    }

    private async Task Register(string email, string userName, string firstName, string lastName)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName,
            lastName,
            userName
        }));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Register failed. Status={(int)response.StatusCode} {response.StatusCode}. Body={body}");
        }
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

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Login failed. Status={(int)response.StatusCode} {response.StatusCode}. Body={body}");
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        return auth!.accessToken;
    }

    private sealed record AuthResponse(
        Guid userId,
        string accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    private sealed record OnboardingStatusDto(
        bool IsCompleted,
        bool NeedsOnboarding,
        bool HasCompletedContactsStep,
        bool SkippedContactsImport,
        DateTime? CompletedAt,
        bool CanFinishOnboarding);

    private sealed record ProfileCompletenessDto(
        bool HasPhoneNumber,
        bool HasProfilePhoto,
        bool HasActiveGroup);

    private sealed record MyGroupPreviewDto(
        Guid Id,
        string Name,
        string? AvatarUrl,
        bool IsActiveGroup);

    private sealed record MyPendingInvitationPreviewDto(
        Guid InvitationId,
        Guid GroupId,
        string GroupName,
        string? GroupAvatarUrl,
        Guid InvitedByUserId,
        string InvitedByUserName,
        string? InvitedByUserProfilePhotoUrl,
        DateTime CreatedAt);

    private sealed record MeDto(
        Guid Id,
        string Email,
        string UserName,
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? ProfilePhotoUrl,
        Guid? ActiveGroupId,
        DateTime CreatedAt,
        OnboardingStatusDto Onboarding,
        ProfileCompletenessDto ProfileCompleteness,
        int GroupsCount,
        int PendingInvitationsCount,
        IReadOnlyList<MyGroupPreviewDto> Groups,
        IReadOnlyList<MyPendingInvitationPreviewDto> PendingInvitations);
}