namespace InMoment.Application.Features.Uploads.PresignPhotoUpload;

public sealed record PresignPhotoUploadResponse(
    string UploadUrl,
    string StorageKey,
    string FileUrl,
    DateTimeOffset ExpiresAt
);