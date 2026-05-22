using MediatR;

namespace InMoment.Application.Features.Media.Saved.ListSavedPhotos;

public sealed record ListSavedPhotosQuery(
    int Limit = 20,
    string? Cursor = null
) : IRequest<SavedPhotosPageDto>;