using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Memories.GetGroupStats;

public sealed class GetGroupMemoriesStatsHandler
    : IRequestHandler<GetGroupMemoriesStatsQuery, GroupMemoriesStatsDto>
{
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly ICurrentUser _current;

    public GetGroupMemoriesStatsHandler(
        IGroupRepository groups,
        IPhotoRepository photos,
        ICurrentUser current)
    {
        _groups = groups;
        _photos = photos;
        _current = current;
    }

    public async Task<GroupMemoriesStatsDto> Handle(GetGroupMemoriesStatsQuery q, CancellationToken ct)
    {
        if (q.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var group = await _groups.GetByIdAsync(q.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);

        var postingDates = await _photos.GetPostingDatesByGroupAsync(q.GroupId, ct);
        var totalPhotos = await _photos.CountByGroupAsync(q.GroupId, ct);

        if (postingDates.Count == 0)
        {
            return new GroupMemoriesStatsDto(
                GroupId: q.GroupId,
                TotalPhotos: 0,
                ActiveDays: 0,
                CurrentStreakDays: 0,
                LongestStreakDays: 0,
                LastPostDateUtc: null);
        }

        var activeDays = postingDates.Count;
        var lastPostDate = postingDates[^1];

        var longestStreak = CalculateLongestStreak(postingDates);
        var currentStreak = CalculateCurrentStreak(postingDates);

        return new GroupMemoriesStatsDto(
            GroupId: q.GroupId,
            TotalPhotos: totalPhotos,
            ActiveDays: activeDays,
            CurrentStreakDays: currentStreak,
            LongestStreakDays: longestStreak,
            LastPostDateUtc: lastPostDate);
    }

    private static int CalculateLongestStreak(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
            return 0;

        var longest = 1;
        var current = 1;

        for (var i = 1; i < dates.Count; i++)
        {
            var diff = dates[i].DayNumber - dates[i - 1].DayNumber;

            if (diff == 1)
            {
                current++;
            }
            else if (diff > 1)
            {
                current = 1;
            }

            if (current > longest)
                longest = current;
        }

        return longest;
    }

    private static int CalculateCurrentStreak(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var yesterday = today.AddDays(-1);
        var last = dates[^1];

        if (last != today && last != yesterday)
            return 0;

        var streak = 1;

        for (var i = dates.Count - 1; i > 0; i--)
        {
            var diff = dates[i].DayNumber - dates[i - 1].DayNumber;

            if (diff == 1)
                streak++;
            else
                break;
        }

        return streak;
    }
}