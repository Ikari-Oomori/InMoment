using InMoment.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Test.Common.Persistence;

public static class SqliteDbContextFactory
{
    public static SqliteTestDb Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        return new SqliteTestDb(connection, db);
    }
}

public sealed class SqliteTestDb : IAsyncDisposable
{
    public SqliteConnection Connection { get; }
    public AppDbContext DbContext { get; }

    public SqliteTestDb(SqliteConnection connection, AppDbContext dbContext)
    {
        Connection = connection;
        DbContext = dbContext;
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Connection.DisposeAsync();
    }
}