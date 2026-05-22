using InMoment.Application.Abstractions.Communication;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class DevAccountDeletionRequestSender : IAccountDeletionRequestSender
{
    private readonly ILogger<DevAccountDeletionRequestSender> _logger;

    public DevAccountDeletionRequestSender(
        ILogger<DevAccountDeletionRequestSender> logger)
    {
        _logger = logger;
    }

    public Task SendReceivedAsync(
        string email,
        string displayName,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "DEV ACCOUNT DELETION REQUEST EMAIL. Email: {Email}; User: {DisplayName}",
            email,
            displayName);

        return Task.CompletedTask;
    }
}