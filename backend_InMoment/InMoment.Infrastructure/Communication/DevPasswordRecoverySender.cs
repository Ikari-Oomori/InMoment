using InMoment.Application.Abstractions.Communication;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class DevPasswordRecoverySender : IPasswordRecoverySender
{
    private readonly PasswordRecoveryLinkBuilder _linkBuilder;
    private readonly ILogger<DevPasswordRecoverySender> _logger;

    public DevPasswordRecoverySender(
        PasswordRecoveryLinkBuilder linkBuilder,
        ILogger<DevPasswordRecoverySender> logger)
    {
        _linkBuilder = linkBuilder;
        _logger = logger;
    }

    public Task SendResetPasswordAsync(
        string email,
        string displayName,
        string rawToken,
        CancellationToken ct)
    {
        var links = _linkBuilder.Build(rawToken);

        _logger.LogWarning(
            """
DEV PASSWORD RESET TOKEN.
Email: {Email}
User: {DisplayName}
Token: {Token}
AppResetLink: {AppResetLink}
WebResetLink: {WebResetLink}
""",
            email,
            displayName,
            links.RawToken,
            links.AppResetLink,
            links.WebResetLink ?? "(not configured)");

        return Task.CompletedTask;
    }
}