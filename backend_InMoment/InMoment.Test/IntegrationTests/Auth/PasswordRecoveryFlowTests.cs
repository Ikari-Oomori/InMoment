using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InMoment.IntegrationTests.Common;
using InMoment.IntegrationTests.Factory;

namespace InMoment.IntegrationTests.Auth;

public sealed class PasswordRecoveryFlowTests : TestBase, IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PasswordRecoveryFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnNoContent_ForExistingAndMissingEmail()
    {
        var email = $"forgot_{Guid.NewGuid():N}@test.com";
        var userName = $"forgot_{Guid.NewGuid():N}";
        await Register(email, userName);

        var existingResponse = await _client.PostAsync("/api/auth/forgot-password", Json(new
        {
            email
        }));

        var missingResponse = await _client.PostAsync("/api/auth/forgot-password", Json(new
        {
            email = $"missing_{Guid.NewGuid():N}@test.com"
        }));

        existingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ShouldReturnForbidden()
    {
        var response = await _client.PostAsync("/api/auth/reset-password", Json(new
        {
            token = "definitely-invalid-token",
            newPassword = "NewPass123!"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task Register(string email, string userName)
    {
        var response = await _client.PostAsync("/api/auth/register", Json(new
        {
            email,
            password = "Pass123!",
            firstName = "Reset",
            lastName = "User",
            userName
        }));

        response.EnsureSuccessStatusCode();
    }
}