namespace InMoment.Infrastructure.Communication;

public sealed record PasswordRecoveryLinks(
    string AppResetLink,
    string? WebResetLink,
    string RawToken);