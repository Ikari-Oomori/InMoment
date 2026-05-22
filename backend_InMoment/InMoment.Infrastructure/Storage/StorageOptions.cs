namespace InMoment.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; init; } = default!;
    public string AccessKey { get; init; } = default!;
    public string SecretKey { get; init; } = default!;
    public string Bucket { get; init; } = default!;
    public string PublicBaseUrl { get; init; } = default!;
    public string Region { get; init; } = "auto";
    public int PresignExpiryMinutes { get; init; } = 10;
}