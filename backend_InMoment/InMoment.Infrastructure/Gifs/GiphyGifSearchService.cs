using System.Text.Json;
using InMoment.Application.Abstractions.Gifs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Gifs;

public sealed class GiphyGifSearchService : IGifSearchService
{
    private readonly HttpClient _httpClient;
    private readonly GiphyOptions _options;
    private readonly ILogger<GiphyGifSearchService> _logger;

    public GiphyGifSearchService(
        HttpClient httpClient,
        IOptions<GiphyOptions> options,
        ILogger<GiphyGifSearchService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GifSearchItemDto>> SearchAsync(
        string query,
        int limit,
        CancellationToken ct)
    {
        var safeQuery = (query ?? string.Empty).Trim();
        if (safeQuery.Length == 0)
            safeQuery = "cute";

        var safeLimit = Math.Clamp(limit, 1, 30);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return Array.Empty<GifSearchItemDto>();

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/search" +
            $"?api_key={Uri.EscapeDataString(_options.ApiKey)}" +
            $"&q={Uri.EscapeDataString(safeQuery)}" +
            $"&limit={safeLimit}" +
            $"&rating={Uri.EscapeDataString(_options.Rating)}" +
            "&lang=ru";

        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GifSearchItemDto>();
            }

            var result = new List<GifSearchItemDto>();

            foreach (var item in data.EnumerateArray())
            {
                var id = ReadString(item, "id");
                var title = ReadString(item, "title");

                if (!item.TryGetProperty("images", out var images))
                    continue;

                var previewUrl =
                    ReadImageUrl(images, "fixed_width_small") ??
                    ReadImageUrl(images, "preview_gif") ??
                    ReadImageUrl(images, "downsized_small");

                var gifUrl =
                    ReadImageUrl(images, "downsized_medium") ??
                    ReadImageUrl(images, "downsized") ??
                    ReadImageUrl(images, "fixed_width") ??
                    previewUrl;

                if (string.IsNullOrWhiteSpace(id) ||
                    string.IsNullOrWhiteSpace(previewUrl) ||
                    string.IsNullOrWhiteSpace(gifUrl))
                {
                    continue;
                }

                result.Add(new GifSearchItemDto(
                    Id: id,
                    Title: string.IsNullOrWhiteSpace(title) ? "GIF" : title,
                    PreviewUrl: previewUrl,
                    GifUrl: gifUrl));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search GIFs via GIPHY.");
            return Array.Empty<GifSearchItemDto>();
        }
    }

    private static string? ReadImageUrl(JsonElement images, string name)
    {
        if (!images.TryGetProperty(name, out var image))
            return null;

        return ReadString(image, "url");
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}