using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Uploads.PresignSystemAnnouncementMediaUpload;

public sealed class PresignSystemAnnouncementMediaUploadHandler
    : IRequestHandler<PresignSystemAnnouncementMediaUploadCommand, PresignSystemAnnouncementMediaUploadResponse>
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
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly IFileStorage _storage;

    public PresignSystemAnnouncementMediaUploadHandler(
        ICurrentUser currentUser,
        ISystemModeratorAccess moderatorAccess,
        IFileStorage storage)
    {
        _currentUser = currentUser;
        _moderatorAccess = moderatorAccess;
        _storage = storage;
    }

    public async Task<PresignSystemAnnouncementMediaUploadResponse> Handle(
        PresignSystemAnnouncementMediaUploadCommand request,
        CancellationToken ct)
    {
        if (_currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        _moderatorAccess.EnsureModerator(_currentUser.UserId);

        var contentType = (request.ContentType ?? string.Empty).Trim();

        if (!ContentTypeToExt.TryGetValue(contentType, out var ext))
            throw new ValidationException("Unsupported content type.");

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var storageKey =
            $"system-announcements/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        var presign = await _storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest(
                Key: storageKey,
                ContentType: contentType),
            ct);

        return new PresignSystemAnnouncementMediaUploadResponse(
            presign.UploadUrl,
            presign.Key,
            presign.FileUrl,
            presign.ExpiresAt);
    }
}