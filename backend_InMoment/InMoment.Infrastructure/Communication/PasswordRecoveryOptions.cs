namespace InMoment.Infrastructure.Communication;

public sealed class PasswordRecoveryOptions
{
    public string? ResetLinkBaseUrl { get; set; }
    public string? WebResetLinkBaseUrl { get; set; }

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpEnableSsl { get; set; } = true;
    public string? SmtpSocketOptions { get; set; }

    public string? SmtpUserName { get; set; }
    public string? SmtpPassword { get; set; }

    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }

    public bool IsSmtpConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        SmtpPort > 0 &&
        !string.IsNullOrWhiteSpace(SmtpUserName) &&
        !string.IsNullOrWhiteSpace(SmtpPassword) &&
        !string.IsNullOrWhiteSpace(SenderEmail);
}