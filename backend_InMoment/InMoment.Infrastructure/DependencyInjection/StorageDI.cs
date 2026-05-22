using Amazon.Runtime;
using Amazon.S3;
using InMoment.Application.Abstractions.Media;
using InMoment.Application.Abstractions.Storage;
using InMoment.Infrastructure.Media;
using InMoment.Infrastructure.Storage;
using InMoment.Infrastructure.SystemMemories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InMoment.Infrastructure.DependencyInjection;

public static class StorageDI
{
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<StorageOptions>(
            config.GetSection(StorageOptions.SectionName));

        services.Configure<VideoProcessingOptions>(
            config.GetSection(VideoProcessingOptions.SectionName));

        var options = config
            .GetSection(StorageOptions.SectionName)
            .Get<StorageOptions>()
            ?? throw new InvalidOperationException("Storage configuration section is missing.");

        ValidateStorageOptions(options);

        var endpoint = options.Endpoint.Trim().TrimEnd('/');
        var useHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var credentials = new BasicAWSCredentials(
                options.AccessKey,
                options.SecretKey);

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                UseHttp = useHttp,
                AuthenticationRegion = options.Region
            };

            return new AmazonS3Client(credentials, s3Config);
        });

        services.AddScoped<IFileStorage, S3FileStorage>();
        services.AddScoped<IVideoProcessingService, FfmpegVideoProcessingService>();
        services.AddScoped<ISystemMemoryVideoRenderService, FfmpegSystemMemoryVideoRenderService>();

        return services;
    }

    private static void ValidateStorageOptions(StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new InvalidOperationException("Storage:Endpoint is not configured.");

        if (string.IsNullOrWhiteSpace(options.AccessKey))
            throw new InvalidOperationException("Storage:AccessKey is not configured.");

        if (string.IsNullOrWhiteSpace(options.SecretKey))
            throw new InvalidOperationException("Storage:SecretKey is not configured.");

        if (string.IsNullOrWhiteSpace(options.Bucket))
            throw new InvalidOperationException("Storage:Bucket is not configured.");

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            throw new InvalidOperationException("Storage:PublicBaseUrl is not configured.");

        if (options.PresignExpiryMinutes <= 0)
            throw new InvalidOperationException("Storage:PresignExpiryMinutes must be greater than zero.");
    }
}