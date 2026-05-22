namespace InMoment.Application.Features.Uploads.PresignSystemAnnouncementMediaUpload;

public sealed record PresignSystemAnnouncementMediaUploadResponse(
    string UploadUrl,
    string StorageKey,
    string FileUrl,
    DateTimeOffset ExpiresAt
);