namespace InMoment.Application.Features.Memories.GetPersonalStats;

public sealed record PersonalMemoriesStatsDto(
    int TotalPhotos,
    int ActiveDays,
    int CurrentStreakDays,
    int LongestStreakDays,
    DateOnly? LastPostDateUtc
);