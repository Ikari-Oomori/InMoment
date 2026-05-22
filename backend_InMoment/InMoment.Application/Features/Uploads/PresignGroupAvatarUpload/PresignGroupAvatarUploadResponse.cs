namespace InMoment.Application.Features.Uploads.PresignGroupAvatarUpload;

public sealed record PresignGroupAvatarUploadResponse(
    string UploadUrl,
    string StorageKey,
    string FileUrl,
    DateTimeOffset ExpiresAt
);