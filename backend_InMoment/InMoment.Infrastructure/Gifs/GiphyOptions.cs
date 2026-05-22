namespace InMoment.Infrastructure.Gifs;

public sealed class GiphyOptions
{
    public const string SectionName = "Giphy";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.giphy.com/v1/gifs";
    public string Rating { get; init; } = "pg-13";
}