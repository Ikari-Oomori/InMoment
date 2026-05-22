namespace InMoment.Application.Features.Memories.GetGroupStats;

public sealed record GroupMemoriesStatsDto(
    Guid GroupId,
    int TotalPhotos,
    int ActiveDays,
    int CurrentStreakDays,
    int LongestStreakDays,
    DateOnly? LastPostDateUtc
);