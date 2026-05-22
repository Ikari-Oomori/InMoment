using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Communication;

public sealed class PasswordRecoveryLinkBuilder
{
    private readonly PasswordRecoveryOptions _options;

    public PasswordRecoveryLinkBuilder(IOptions<PasswordRecoveryOptions> options)
    {
        _options = options.Value;
    }

    public PasswordRecoveryLinks Build(string rawToken)
    {
        var appBase = string.IsNullOrWhiteSpace(_options.ResetLinkBaseUrl)
            ? "inmoment://reset-password"
            : _options.ResetLinkBaseUrl.Trim();

        var webBase = string.IsNullOrWhiteSpace(_options.WebResetLinkBaseUrl)
            ? null
            : _options.WebResetLinkBaseUrl.Trim();

        return new PasswordRecoveryLinks(
            AppResetLink: AppendToken(appBase, rawToken),
            WebResetLink: webBase is null ? null : AppendToken(webBase, rawToken),
            RawToken: rawToken);
    }

    private static string AppendToken(string baseUrl, string rawToken)
    {
        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }
}