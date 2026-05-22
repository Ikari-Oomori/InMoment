using InMoment.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace InMoment.Infrastructure.Persistence;

public sealed class EfAppTransaction : IAppTransaction
{
    private readonly IDbContextTransaction _transaction;

    public EfAppTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken ct)
        => _transaction.CommitAsync(ct);

    public ValueTask DisposeAsync()
        => _transaction.DisposeAsync();
}