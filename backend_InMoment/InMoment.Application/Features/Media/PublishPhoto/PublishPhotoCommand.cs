using MediatR;

namespace InMoment.Application.Features.Media.PublishPhoto;

public sealed record PublishPhotoCommand(
    Guid GroupId,
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string? Caption = null,
    long? TrimStartMs = null,
    long? TrimEndMs = null
) : IRequest<Guid>;