namespace InMoment.Application.Abstractions.Gifs;

public interface IGifSearchService
{
    Task<IReadOnlyList<GifSearchItemDto>> SearchAsync(
        string query,
        int limit,
        CancellationToken ct);
}