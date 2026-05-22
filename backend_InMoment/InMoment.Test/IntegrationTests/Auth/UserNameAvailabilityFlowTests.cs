using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Auth;

public sealed class UserNameAvailabilityFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserNameAvailabilityFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UsernameAvailability_ShouldReturnTrue_WhenUserNameIsFree()
    {
        var userName = $"free_{Guid.NewGuid():N}";

        var response = await _client.GetAsync($"/api/auth/username-availability?userName={userName}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<UserNameAvailabilityDto>();
        dto.Should().NotBeNull();
        dto!.UserName.Should().Be(userName);
        dto.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task UsernameAvailability_ShouldReturnFalse_WhenUserNameAlreadyExists()
    {
        var email = $"taken_{Guid.NewGuid():N}@test.com";
        var userName = $"taken_{Guid.NewGuid():N}";

        await Register(email, userName);

        var response = await _client.GetAsync($"/api/auth/username-availability?userName={userName}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<UserNameAvailabilityDto>();
        dto.Should().NotBeNull();
        dto!.UserName.Should().Be(userName);
        dto.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task UsernameAvailability_ShouldTrimUserName()
    {
        var response = await _client.GetAsync("/api/auth/username-availability?userName=%20%20trim_me%20%20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<UserNameAvailabilityDto>();
        dto.Should().NotBeNull();
        dto!.UserName.Should().Be("trim_me");
        dto.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task UsernameAvailability_ShouldReturnBadRequest_WhenUserNameInvalid()
    {
        var response = await _client.GetAsync("/api/auth/username-availability?userName=!");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UsernameAvailability_ShouldBeAvailableWithoutAuthorization()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/auth/username-availability?userName=public_check");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task Register(string email, string userName)
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
    }

    private sealed record UserNameAvailabilityDto(
        string UserName,
        bool IsAvailable);
}