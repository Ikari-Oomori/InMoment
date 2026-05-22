using MailKit.Security;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Communication;

public sealed class SmtpTransportSettingsFactory
{
    private readonly PasswordRecoveryOptions _options;

    public SmtpTransportSettingsFactory(IOptions<PasswordRecoveryOptions> options)
    {
        _options = options.Value;
    }

    public SmtpTransportSettings Create()
    {
        if (!_options.IsSmtpConfigured)
        {
            throw new InvalidOperationException(
                "SMTP transport is not configured. Check PasswordRecovery settings.");
        }

        var host = _options.SmtpHost!.Trim();
        var userName = _options.SmtpUserName!.Trim();
        var password = _options.SmtpPassword!;
        var senderEmail = _options.SenderEmail!.Trim();
        var senderName = string.IsNullOrWhiteSpace(_options.SenderName)
            ? "InMoment"
            : _options.SenderName.Trim();

        return new SmtpTransportSettings(
            Host: host,
            Port: _options.SmtpPort,
            EnableSsl: _options.SmtpEnableSsl,
            SocketOptions: ResolveSocketOptions(_options.SmtpSocketOptions, _options.SmtpEnableSsl, _options.SmtpPort),
            UserName: userName,
            Password: password,
            SenderEmail: senderEmail,
            SenderName: senderName);
    }

    private static SecureSocketOptions ResolveSocketOptions(
        string? configuredValue,
        bool enableSsl,
        int port)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue) &&
            Enum.TryParse<SecureSocketOptions>(configuredValue, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (!enableSsl)
        {
            return SecureSocketOptions.None;
        }

        return port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            2525 => SecureSocketOptions.StartTls,
            25 => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.Auto
        };
    }
}