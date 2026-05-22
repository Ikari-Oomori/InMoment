namespace InMoment.Application.Features.Uploads.PresignProfilePhotoUpload;

public sealed record PresignProfilePhotoUploadResponse(
    string UploadUrl,
    string StorageKey,
    string FileUrl,
    DateTimeOffset ExpiresAt
);