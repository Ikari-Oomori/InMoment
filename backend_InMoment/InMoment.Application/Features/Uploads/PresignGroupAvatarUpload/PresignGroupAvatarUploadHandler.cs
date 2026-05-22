using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Uploads.PresignGroupAvatarUpload;

public sealed class PresignGroupAvatarUploadHandler
    : IRequestHandler<PresignGroupAvatarUploadCommand, PresignGroupAvatarUploadResponse>
{
    private static readonly Dictionary<string, string> ContentTypeToExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/heic"] = "heic",
        ["image/heif"] = "heif",
    };

    private readonly IGroupRepository _groups;
    private readonly ICurrentUser _current;
    private readonly IFileStorage _storage;

    public PresignGroupAvatarUploadHandler(
        IGroupRepository groups,
        ICurrentUser current,
        IFileStorage storage)
    {
        _groups = groups;
        _current = current;
        _storage = storage;
    }

    public async Task<PresignGroupAvatarUploadResponse> Handle(PresignGroupAvatarUploadCommand cmd, CancellationToken ct)
    {
        if (cmd.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureManager(_current.UserId);

        var contentType = (cmd.ContentType ?? string.Empty).Trim();
        if (!ContentTypeToExt.TryGetValue(contentType, out var ext))
            throw new ValidationException("Unsupported content type.");

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var storageKey = $"groups/{cmd.GroupId}/avatar/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}";

        var presign = await _storage.GetPresignedUploadUrlAsync(
            new PresignedUploadRequest(
                Key: storageKey,
                ContentType: contentType),
            ct);

        return new PresignGroupAvatarUploadResponse(
            presign.UploadUrl,
            presign.Key,
            presign.FileUrl,
            presign.ExpiresAt);
    }
}