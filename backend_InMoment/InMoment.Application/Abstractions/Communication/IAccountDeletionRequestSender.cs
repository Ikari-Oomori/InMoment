namespace InMoment.Application.Abstractions.Communication;

public interface IAccountDeletionRequestSender
{
    Task SendReceivedAsync(
        string email,
        string displayName,
        CancellationToken ct);
}