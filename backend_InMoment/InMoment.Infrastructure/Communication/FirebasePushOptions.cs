namespace InMoment.Infrastructure.Communication;

public sealed class FirebasePushOptions
{
    public const string SectionName = "FirebasePush";

    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountJsonPath { get; set; } = string.Empty;
}