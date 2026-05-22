namespace InMoment.Application.Abstractions.Communication;

public interface IPasswordRecoverySender
{
    Task SendResetPasswordAsync(
        string email,
        string displayName,
        string rawToken,
        CancellationToken ct);
}