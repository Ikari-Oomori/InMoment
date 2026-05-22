using Amazon.Runtime;
using Amazon.S3;
using FluentAssertions;
using InMoment.Application.Abstractions.Storage;
using InMoment.Infrastructure.DependencyInjection;
using InMoment.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Tests.DependencyInjection;

public sealed class StorageDITests
{
    [Fact]
    public void AddStorage_ShouldRegisterExpectedServices_AndBindOptions()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Endpoint"] = "http://localhost:9000/",
                ["Storage:AccessKey"] = "minio",
                ["Storage:SecretKey"] = "minio123",
                ["Storage:Bucket"] = "inmoment",
                ["Storage:PublicBaseUrl"] = "http://localhost:9000/inmoment",
                ["Storage:Region"] = "auto",
                ["Storage:PresignExpiryMinutes"] = "15"
            })
            .Build();

        services.AddStorage(configuration);

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var amazonS3 = provider.GetRequiredService<IAmazonS3>();
        var storage = provider.GetRequiredService<IFileStorage>();

        options.Endpoint.Should().Be("http://localhost:9000/");
        options.AccessKey.Should().Be("minio");
        options.SecretKey.Should().Be("minio123");
        options.Bucket.Should().Be("inmoment");
        options.PublicBaseUrl.Should().Be("http://localhost:9000/inmoment");
        options.PresignExpiryMinutes.Should().Be(15);

        amazonS3.Should().NotBeNull();
        storage.Should().BeOfType<S3FileStorage>();
    }
}