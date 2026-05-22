using FluentAssertions;
using InMoment.Infrastructure.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InMoment.Infrastructure.Tests.Communication;

public sealed class DevPasswordRecoverySenderTests
{
    [Fact]
    public async Task SendResetPasswordAsync_ShouldWriteWarningLog_AndComplete()
    {
        var logger = new Mock<ILogger<DevPasswordRecoverySender>>();

        var options = Options.Create(new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = "inmoment://reset-password"
        });

        var linkBuilder = new PasswordRecoveryLinkBuilder(options);
        var sender = new DevPasswordRecoverySender(linkBuilder, logger.Object);

        await sender.SendResetPasswordAsync(
            "user@test.com",
            "Test User",
            "raw-reset-token",
            CancellationToken.None);

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("DEV PASSWORD RESET TOKEN") &&
                    state.ToString()!.Contains("user@test.com") &&
                    state.ToString()!.Contains("Test User") &&
                    state.ToString()!.Contains("raw-reset-token") &&
                    state.ToString()!.Contains("inmoment://reset-password")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}