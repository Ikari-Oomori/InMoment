using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Uploads.PresignPhotoUpload;

public sealed class PresignPhotoUploadHandler
    : IRequestHandler<PresignPhotoUploadCommand, PresignPhotoUploadResponse>
{
    private static readonly Dictionary<string, string> ContentTypeToExt =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = "jpg",
            ["image/png"] = "png",
            ["image/webp"] = "webp",
            ["image/heic"] = "heic",
            ["image/heif"] = "heif",

            ["video/mp4"] = "mp4",
            ["video/quicktime"] = "mov",
            ["video/x-m4v"] = "m4v",
            ["video/webm"] = "webm",
            ["video/3gpp"] = "3gp",
        };

    private readonly ICurrentUser _currentUser;
    private readonly IGroupRepository _groups;
    private readonly IFileStorage _storage;

    public PresignPhotoUploadHandler(
        ICurrentUser currentUser,
        IGroupRepository groups,
        IFileStorage storage)
    {
        _currentUser = currentUser;
        _groups = groups;
        _storage = storage;
    }

    public async Task<PresignPhotoUploadResponse> Handle(
        PresignPhotoUploadCommand request,
        CancellationToken ct)
    {
        if (request.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var contentType = (request.ContentType ?? string.Empty).Trim();
        if (!ContentTypeToExt.TryGetValue(contentType, out var ext))
            throw new ValidationException("Unsupported content type.");

        var userId = _currentUser.UserId;

        var isMember = await _groups.IsMemberAsync(request.GroupId, userId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var storageKey =
            $"groups/{request.GroupId}/photos/{userId}/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        var presign = await _storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest(
                Key: storageKey,
                ContentType: contentType),
            ct);

        return new PresignPhotoUploadResponse(
            presign.UploadUrl,
            presign.Key,
            presign.FileUrl,
            presign.ExpiresAt);
    }
}
