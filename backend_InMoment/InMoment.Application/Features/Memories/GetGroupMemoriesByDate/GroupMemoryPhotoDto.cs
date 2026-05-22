namespace InMoment.Application.Features.Memories.GetGroupMemoriesByDate;

public sealed record GroupMemoryPhotoDto(
    Guid PhotoId,
    string PhotoUrl,
    DateTime CreatedAt,
    Guid UploadedByUserId
);

public sealed record GroupMemoriesByDateDto(
    Guid GroupId,
    DateOnly Date,
    IReadOnlyList<GroupMemoryPhotoDto> Items
);