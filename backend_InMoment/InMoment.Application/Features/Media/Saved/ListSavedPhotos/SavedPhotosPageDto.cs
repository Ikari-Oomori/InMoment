namespace InMoment.Application.Features.Media.Saved.ListSavedPhotos;

public sealed record SavedPhotoItemDto(
    Guid PhotoId,
    Guid GroupId,
    string GroupName,
    string? GroupAvatarUrl,
    Guid UploadedByUserId,
    string UploadedByUserName,
    string? UploadedByUserProfilePhotoUrl,
    bool IsMine,
    string PhotoUrl,
    string ContentType,
    long SizeBytes,
    string? Caption,
    DateTime PhotoCreatedAt,
    DateTime SavedAt
);

public sealed record SavedPhotosPageDto(
    IReadOnlyList<SavedPhotoItemDto> Items,
    string? NextCursor
);