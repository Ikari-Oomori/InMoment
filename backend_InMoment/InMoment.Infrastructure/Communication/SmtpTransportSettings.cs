using MailKit.Security;

namespace InMoment.Infrastructure.Communication;

public sealed record SmtpTransportSettings(
    string Host,
    int Port,
    bool EnableSsl,
    SecureSocketOptions SocketOptions,
    string UserName,
    string Password,
    string SenderEmail,
    string SenderName);