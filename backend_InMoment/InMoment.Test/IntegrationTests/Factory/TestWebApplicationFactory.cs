using InMoment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace InMoment.IntegrationTests.Factory;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        Environment.SetEnvironmentVariable("ConnectionStrings__Db",
            "Host=localhost;Database=inmoment_test;Username=test;Password=test");

        Environment.SetEnvironmentVariable("Jwt__Issuer", "InMoment.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "InMoment.Tests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "12345678901234567890123456789012");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");

        Environment.SetEnvironmentVariable("Storage__Endpoint", "http://localhost:9000");
        Environment.SetEnvironmentVariable("Storage__AccessKey", "test");
        Environment.SetEnvironmentVariable("Storage__SecretKey", "test");
        Environment.SetEnvironmentVariable("Storage__Bucket", "inmoment-test");
        Environment.SetEnvironmentVariable("Storage__PublicBaseUrl", "http://localhost:9000/inmoment-test");
        Environment.SetEnvironmentVariable("Storage__Region", "us-east-1");
        Environment.SetEnvironmentVariable("Storage__PresignExpiryMinutes", "10");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            var dbName = $"inmoment-tests-{Guid.NewGuid():N}";
            _connection = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        });

        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Db", null);

        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", null);

        Environment.SetEnvironmentVariable("Storage__Endpoint", null);
        Environment.SetEnvironmentVariable("Storage__AccessKey", null);
        Environment.SetEnvironmentVariable("Storage__SecretKey", null);
        Environment.SetEnvironmentVariable("Storage__Bucket", null);
        Environment.SetEnvironmentVariable("Storage__PublicBaseUrl", null);
        Environment.SetEnvironmentVariable("Storage__Region", null);
        Environment.SetEnvironmentVariable("Storage__PresignExpiryMinutes", null);

        _connection?.Dispose();
        _connection = null;

        base.Dispose(disposing);
    }
}