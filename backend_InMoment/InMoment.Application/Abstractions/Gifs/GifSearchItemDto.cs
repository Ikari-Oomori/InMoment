namespace InMoment.Application.Abstractions.Gifs;

public sealed record GifSearchItemDto(
    string Id,
    string Title,
    string PreviewUrl,
    string GifUrl
);