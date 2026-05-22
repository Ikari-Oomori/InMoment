using InMoment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace InMoment.Tests.Infrastructure.Tests.Persistence;

public sealed class EfAppTransactionTests
{
    [Fact]
    public async Task CommitAsync_ShouldDelegateToInnerTransaction()
    {
        var inner = new Mock<IDbContextTransaction>();

        var transaction = new EfAppTransaction(inner.Object);

        await transaction.CommitAsync(CancellationToken.None);

        inner.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDelegateToInnerTransaction()
    {
        var inner = new Mock<IDbContextTransaction>();

        var transaction = new EfAppTransaction(inner.Object);

        await transaction.DisposeAsync();

        inner.Verify(x => x.DisposeAsync(), Times.Once);
    }
}