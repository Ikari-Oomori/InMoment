using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Uploads.PresignProfilePhotoUpload;

public sealed class PresignProfilePhotoUploadHandler
    : IRequestHandler<PresignProfilePhotoUploadCommand, PresignProfilePhotoUploadResponse>
{
    private static readonly Dictionary<string, string> ContentTypeToExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    private readonly ICurrentUser _current;
    private readonly IFileStorage _storage;

    public PresignProfilePhotoUploadHandler(ICurrentUser current, IFileStorage storage)
    {
        _current = current;
        _storage = storage;
    }

    public async Task<PresignProfilePhotoUploadResponse> Handle(PresignProfilePhotoUploadCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var contentType = (cmd.ContentType ?? string.Empty).Trim();
        if (!ContentTypeToExt.TryGetValue(contentType, out var ext))
            throw new ValidationException("Unsupported content type.");

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var storageKey = $"users/{_current.UserId}/profile-photo/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        var presign = await _storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest(Key: storageKey, ContentType: contentType),
            ct);

        return new PresignProfilePhotoUploadResponse(
            presign.UploadUrl,
            presign.Key,
            presign.FileUrl,
            presign.ExpiresAt);
    }
}