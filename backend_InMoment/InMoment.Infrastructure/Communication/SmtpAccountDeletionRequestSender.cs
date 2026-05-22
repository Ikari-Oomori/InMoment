using InMoment.Application.Abstractions.Communication;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class SmtpAccountDeletionRequestSender : IAccountDeletionRequestSender
{
    private readonly SmtpTransportSettingsFactory _transportSettingsFactory;
    private readonly ILogger<SmtpAccountDeletionRequestSender> _logger;

    public SmtpAccountDeletionRequestSender(
        SmtpTransportSettingsFactory transportSettingsFactory,
        ILogger<SmtpAccountDeletionRequestSender> logger)
    {
        _transportSettingsFactory = transportSettingsFactory;
        _logger = logger;
    }

    public async Task SendReceivedAsync(
        string email,
        string displayName,
        CancellationToken ct)
    {
        var transport = _transportSettingsFactory.Create();

        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(
            transport.SenderName,
            transport.SenderEmail));
        message.To.Add(MimeKit.MailboxAddress.Parse(email));
        message.Subject = "InMoment — запрос на удаление аккаунта получен";

        message.Body = new MimeKit.TextPart("plain")
        {
            Text = BuildBody(displayName)
        };

        using var client = new SmtpClient();

        try
        {
            _logger.LogInformation(
                "Connecting to SMTP for account deletion confirmation email. Host: {Host}; Port: {Port}; SocketOptions: {SocketOptions}; EnableSsl: {EnableSsl}; Recipient: {Email}",
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
                "Account deletion confirmation email sent successfully to {Email}.",
                email);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send account deletion confirmation email to {Email}. Host: {Host}; Port: {Port}; SocketOptions: {SocketOptions}; EnableSsl: {EnableSsl}",
                email,
                transport.Host,
                transport.Port,
                transport.SocketOptions,
                transport.EnableSsl);

            throw;
        }
    }

    private static string BuildBody(string displayName)
    {
        var safeName = string.IsNullOrWhiteSpace(displayName)
            ? "пользователь"
            : displayName.Trim();

        return $"""
Здравствуйте, {safeName}!

Мы получили ваш запрос на удаление аккаунта и связанных данных в InMoment.

Запрос зарегистрирован и передан на обработку.
Статус обработки можно отслеживать в приложении.

Если вы не отправляли этот запрос, рекомендуем как можно скорее проверить безопасность аккаунта и активные сессии.

Команда InMoment
""";
    }
}