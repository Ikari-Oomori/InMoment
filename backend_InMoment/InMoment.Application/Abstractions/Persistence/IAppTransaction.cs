namespace InMoment.Application.Abstractions.Persistence;

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}