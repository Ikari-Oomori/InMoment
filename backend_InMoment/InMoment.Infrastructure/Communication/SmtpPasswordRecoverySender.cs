using InMoment.Application.Abstractions.Communication;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class SmtpPasswordRecoverySender : IPasswordRecoverySender
{
    private readonly PasswordRecoveryLinkBuilder _linkBuilder;
    private readonly SmtpTransportSettingsFactory _transportSettingsFactory;
    private readonly ILogger<SmtpPasswordRecoverySender> _logger;

    public SmtpPasswordRecoverySender(
        PasswordRecoveryLinkBuilder linkBuilder,
        SmtpTransportSettingsFactory transportSettingsFactory,
        ILogger<SmtpPasswordRecoverySender> logger)
    {
        _linkBuilder = linkBuilder;
        _transportSettingsFactory = transportSettingsFactory;
        _logger = logger;
    }

    public async Task SendResetPasswordAsync(
        string email,
        string displayName,
        string rawToken,
        CancellationToken ct)
    {
        var links = _linkBuilder.Build(rawToken);
        var transport = _transportSettingsFactory.Create();

        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(
            transport.SenderName,
            transport.SenderEmail));
        message.To.Add(MimeKit.MailboxAddress.Parse(email));
        message.Subject = "InMoment — восстановление пароля";

        message.Body = new MimeKit.TextPart("plain")
        {
            Text = BuildBody(displayName, links)
        };

        using var client = new SmtpClient();

        try
        {
            _logger.LogInformation(
                "Connecting to SMTP for password recovery email. Host: {Host}; Port: {Port}; SocketOptions: {SocketOptions}; EnableSsl: {EnableSsl}; Recipient: {Email}",
                transport.Host,
                transport.Port,
                transport.SocketOptions,
                transport.EnableSsl,
                email);

            await client.ConnectAsync(
                transport.Host,
                transport.Port,
                transport.SocketOptions,
                ct);

            await client.AuthenticateAsync(
                transport.UserName,
                transport.Password,
                ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "Password recovery email sent successfully to {Email}.",
                email);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send password recovery email to {Email}. Host: {Host}; Port: {Port}; SocketOptions: {SocketOptions}; EnableSsl: {EnableSsl}",
                email,
                transport.Host,
                transport.Port,
                transport.SocketOptions,
                transport.EnableSsl);

            throw;
        }
    }

    private static string BuildBody(string displayName, PasswordRecoveryLinks links)
    {
        var safeName = string.IsNullOrWhiteSpace(displayName)
            ? "пользователь"
            : displayName.Trim();

        var webBlock = string.IsNullOrWhiteSpace(links.WebResetLink)
            ? string.Empty
            : $"""

Также можно открыть web-ссылку для сброса:
{links.WebResetLink}
""";

        return $"""
Здравствуйте, {safeName}!

Мы получили запрос на восстановление пароля в InMoment.

Откройте ссылку в приложении:
{links.AppResetLink}{webBlock}

Если ссылка не открылась на этом устройстве, можно вручную вставить токен в приложении:
{links.RawToken}

Срок действия токена ограничен.
Если вы не запрашивали восстановление пароля, просто проигнорируйте это письмо.

Команда InMoment
""";
    }
}