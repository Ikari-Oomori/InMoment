namespace InMoment.Application.Features.Memories.GetPersonalMemoriesByDate;

public sealed record PersonalMemoryPhotoDto(
    Guid PhotoId,
    Guid GroupId,
    string PhotoUrl,
    DateTime CreatedAt
);

public sealed record PersonalMemoriesByDateDto(
    DateOnly Date,
    IReadOnlyList<PersonalMemoryPhotoDto> Items
);