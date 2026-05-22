using InMoment.Application.Abstractions.Communication;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class ResilientAccountDeletionRequestSender : IAccountDeletionRequestSender
{
    private readonly SmtpAccountDeletionRequestSender _primary;
    private readonly DevAccountDeletionRequestSender _fallback;
    private readonly ILogger<ResilientAccountDeletionRequestSender> _logger;

    public ResilientAccountDeletionRequestSender(
        SmtpAccountDeletionRequestSender primary,
        DevAccountDeletionRequestSender fallback,
        ILogger<ResilientAccountDeletionRequestSender> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task SendReceivedAsync(
        string email,
        string displayName,
        CancellationToken ct)
    {
        try
        {
            await _primary.SendReceivedAsync(email, displayName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SMTP account deletion confirmation delivery failed for {Email}. Falling back to DEV logging sender.",
                email);

            await _fallback.SendReceivedAsync(email, displayName, ct);
        }
    }
}