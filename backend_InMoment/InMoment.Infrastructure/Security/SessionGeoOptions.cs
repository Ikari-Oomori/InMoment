namespace InMoment.Infrastructure.Security;

public sealed class SessionGeoOptions
{
    public const string SectionName = "SessionGeo";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://ipwho.is";
}