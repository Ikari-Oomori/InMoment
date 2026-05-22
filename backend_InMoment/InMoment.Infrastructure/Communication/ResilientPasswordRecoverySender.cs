using InMoment.Application.Abstractions.Communication;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class ResilientPasswordRecoverySender : IPasswordRecoverySender
{
    private readonly SmtpPasswordRecoverySender _primary;
    private readonly DevPasswordRecoverySender _fallback;
    private readonly ILogger<ResilientPasswordRecoverySender> _logger;

    public ResilientPasswordRecoverySender(
        SmtpPasswordRecoverySender primary,
        DevPasswordRecoverySender fallback,
        ILogger<ResilientPasswordRecoverySender> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task SendResetPasswordAsync(
        string email,
        string displayName,
        string rawToken,
        CancellationToken ct)
    {
        try
        {
            await _primary.SendResetPasswordAsync(email, displayName, rawToken, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SMTP password recovery delivery failed for {Email}. Falling back to DEV logging sender.",
                email);

            await _fallback.SendResetPasswordAsync(email, displayName, rawToken, ct);
        }
    }
}