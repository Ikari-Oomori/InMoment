using FluentAssertions;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Persistence;
using InMoment.Test.Common.Persistence;

namespace InMoment.Tests.Infrastructure.Tests.Persistence;

public sealed class UnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var uow = new UnitOfWork(db);

        var user = User.Create(
            email: "uow_user@test.com",
            passwordHash: "hash",
            userName: "uow_user",
            firstName: "Uow",
            lastName: "User");

        db.Users.Add(user);

        await uow.SaveChangesAsync(CancellationToken.None);

        var stored = await db.Users.FindAsync(user.Id);
        stored.Should().NotBeNull();
        stored!.Email.Should().Be("uow_user@test.com");
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldReturnWorkingTransaction()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var uow = new UnitOfWork(db);

        await using var tx = await uow.BeginTransactionAsync(CancellationToken.None);

        var user = User.Create(
            email: "tx_user@test.com",
            passwordHash: "hash",
            userName: "tx_user",
            firstName: "Tx",
            lastName: "User");

        db.Users.Add(user);
        await uow.SaveChangesAsync(CancellationToken.None);
        await tx.CommitAsync(CancellationToken.None);

        var stored = await db.Users.FindAsync(user.Id);
        stored.Should().NotBeNull();
        stored!.UserName.Should().Be("tx_user");
    }
}