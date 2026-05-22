using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Notifications;
using InMoment.Domain.SystemMemories;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.SystemMemories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Notifications;

public sealed class SystemNotificationProcessor
{
    private readonly AppDbContext _db;
    private readonly INotificationRepository _notifications;
    private readonly ISystemNotificationStateRepository _states;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly IUnitOfWork _uow;
    private readonly IOptions<SystemNotificationOptions> _options;
    private readonly ISystemMemoryVideoRenderService _memoryVideoRenderService;
    private readonly ILogger<SystemNotificationProcessor> _logger;

    public SystemNotificationProcessor(
        AppDbContext db,
        INotificationRepository notifications,
        ISystemNotificationStateRepository states,
        INotificationPushDeliveryService pushDelivery,
        INotificationRealtime notificationRealtime,
        IUnitOfWork uow,
        IOptions<SystemNotificationOptions> options,
        ISystemMemoryVideoRenderService memoryVideoRenderService,
        ILogger<SystemNotificationProcessor> logger)
    {
        _db = db;
        _notifications = notifications;
        _states = states;
        _pushDelivery = pushDelivery;
        _notificationRealtime = notificationRealtime;
        _uow = uow;
        _options = options;
        _memoryVideoRenderService = memoryVideoRenderService;
        _logger = logger;
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        var options = _options.Value;
        if (!options.Enabled)
            return;

        var nowUtc = DateTime.UtcNow;
        var today = nowUtc.Date;

        var usersQuery = _db.Users
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!options.DevForceSystemMemories)
        {
            usersQuery = usersQuery.Where(x => x.IsOnboardingCompleted);
        }

        var users = await usersQuery.ToListAsync(ct);

        foreach (var user in users)
        {
            try
            {
                var state = await _states.GetByUserIdAsync(user.Id, ct);

                await TrySendShareReminderAsync(user.Id, user.ActiveGroupId, user.OnboardingCompletedAt, state, nowUtc, options, ct);
                await TrySendFeedbackPromptAsync(user.Id, user.OnboardingCompletedAt, state, nowUtc, options, ct);
                await TrySendAnniversaryAsync(user.Id, user.CreatedAt, state, today, nowUtc, ct);
                await TrySendProductAnnouncementAsync(user.Id, state, nowUtc, options, ct);
                await TryCreateSystemMemoriesAsync(user.Id, user.CreatedAt, nowUtc, options, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "System notification processing failed for user {UserId}.",
                    user.Id);
            }
        }
    }

    private async Task TrySendShareReminderAsync(
        Guid userId,
        Guid? activeGroupId,
        DateTime? onboardingCompletedAtUtc,
        SystemNotificationState? state,
        DateTime nowUtc,
        SystemNotificationOptions options,
        CancellationToken ct)
    {
        if (!activeGroupId.HasValue)
            return;

        if (!onboardingCompletedAtUtc.HasValue)
            return;

        var sinceUtc = nowUtc.AddDays(-options.ShareReminderAfterDays);

        if (onboardingCompletedAtUtc.Value > sinceUtc)
            return;

        if (state?.LastShareReminderSentAtUtc.HasValue == true &&
            state.LastShareReminderSentAtUtc.Value > nowUtc.AddDays(-options.ShareReminderCooldownDays))
        {
            return;
        }

        var hasRecentPhoto = await _db.Photos
            .AsNoTracking()
            .AnyAsync(
                x => x.UploadedByUserId == userId &&
                     !x.IsDeleted &&
                     x.CreatedAt >= sinceUtc,
                ct);

        if (hasRecentPhoto)
            return;

        state = await EnsureStateAsync(userId, state, ct);
        state.MarkShareReminderSent(nowUtc);

        var notification = Notification.CreateShareReminder(userId);
        await PersistAndDispatchAsync(notification, userId, ct);
    }

    private async Task TrySendFeedbackPromptAsync(
        Guid userId,
        DateTime? onboardingCompletedAtUtc,
        SystemNotificationState? state,
        DateTime nowUtc,
        SystemNotificationOptions options,
        CancellationToken ct)
    {
        if (!onboardingCompletedAtUtc.HasValue)
            return;

        if (onboardingCompletedAtUtc.Value > nowUtc.AddDays(-options.FeedbackPromptAfterDays))
            return;

        if (state?.LastFeedbackPromptSentAtUtc.HasValue == true &&
            state.LastFeedbackPromptSentAtUtc.Value > nowUtc.AddDays(-options.FeedbackPromptCooldownDays))
        {
            return;
        }

        state = await EnsureStateAsync(userId, state, ct);
        state.MarkFeedbackPromptSent(nowUtc);

        var notification = Notification.CreateFeedbackPrompt(userId);
        await PersistAndDispatchAsync(notification, userId, ct);
    }

    private async Task TrySendAnniversaryAsync(
        Guid userId,
        DateTime createdAtUtc,
        SystemNotificationState? state,
        DateTime todayUtcDate,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (createdAtUtc.Year >= todayUtcDate.Year)
            return;

        if (createdAtUtc.Month != todayUtcDate.Month || createdAtUtc.Day != todayUtcDate.Day)
            return;

        if (state?.LastAnniversaryYearSent == todayUtcDate.Year)
            return;

        state = await EnsureStateAsync(userId, state, ct);
        state.MarkAnniversarySent(todayUtcDate.Year, nowUtc);

        var notification = Notification.CreateAnniversary(userId);
        await PersistAndDispatchAsync(notification, userId, ct);
    }

    private async Task TrySendProductAnnouncementAsync(
        Guid userId,
        SystemNotificationState? state,
        DateTime nowUtc,
        SystemNotificationOptions options,
        CancellationToken ct)
    {
        var announcement = options.ProductAnnouncement;

        if (!announcement.Enabled)
            return;

        var currentKey = announcement.CurrentKey?.Trim();
        if (string.IsNullOrWhiteSpace(currentKey))
            return;

        if (string.Equals(state?.LastProductAnnouncementKey, currentKey, StringComparison.Ordinal))
            return;

        state = await EnsureStateAsync(userId, state, ct);
        state.MarkProductAnnouncementSent(currentKey, nowUtc);

        var notification = Notification.CreateProductAnnouncement(userId);
        await PersistAndDispatchAsync(notification, userId, ct);
    }

    private async Task TryCreateSystemMemoriesAsync(
        Guid userId,
        DateTime userCreatedAtUtc,
        DateTime nowUtc,
        SystemNotificationOptions options,
        CancellationToken ct)
    {
        var devForceSystemMemories = options.DevForceSystemMemories;

        foreach (var period in new[]
                 {
                 SystemMemoryPeriod.ThreeMonths,
                 SystemMemoryPeriod.SixMonths,
                 SystemMemoryPeriod.TwelveMonths
             })
        {
            try
            {
                var months = (int)period;

                if (!devForceSystemMemories && userCreatedAtUtc > nowUtc.AddMonths(-months))
                    continue;

                var periodStart = nowUtc.AddMonths(-months);
                var periodEnd = nowUtc;

                var memory = await _db.SystemMemories
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Period == period, ct);

                var shouldRenderVideo =
                    devForceSystemMemories ||
                    string.IsNullOrWhiteSpace(memory?.GeneratedVideoStorageKey);

                if (!shouldRenderVideo)
                    continue;

                var sourcePhotos = await LoadSourcePhotosAsync(
                    userId,
                    memory,
                    periodStart,
                    periodEnd,
                    devForceSystemMemories,
                    ct);

                var minimumSourcePhotos = devForceSystemMemories ? 1 : 3;
                if (sourcePhotos.Count < minimumSourcePhotos)
                {
                    _logger.LogInformation(
                        "Skipped system memory {Period}m for user {UserId}: only {Count} source media.",
                        months,
                        userId,
                        sourcePhotos.Count);

                    continue;
                }

                if (memory is null)
                {
                    memory = SystemMemory.Create(
                        userId,
                        period,
                        sourcePhotos.Select(x => x.Id).ToList(),
                        sourcePhotos.Last().Id,
                        periodStart,
                        periodEnd,
                        nowUtc);

                    await _db.SystemMemories.AddAsync(memory, ct);
                    await _uow.SaveChangesAsync(ct);
                }

                var renderResult = await _memoryVideoRenderService.RenderAsync(
                    userId,
                    memory.Id,
                    period,
                    sourcePhotos.Select(x => new SystemMemoryVideoSource(
                            x.Id,
                            x.StorageKey,
                            x.ContentType,
                            x.CreatedAt,
                            x.Caption))
                        .ToList(),
                    ct);

                memory.AttachGeneratedVideo(
                    renderResult.StorageKey,
                    renderResult.ContentType,
                    renderResult.SizeBytes);

                await _uow.SaveChangesAsync(ct);

                if (!await HasSystemMemoryNotificationAsync(userId, memory.Id, ct))
                {
                    var notification = Notification.CreateSystemMemoryReady(userId, memory.Id);
                    await PersistAndDispatchAsync(notification, userId, ct);
                }

                _logger.LogInformation(
                    "System memory {Period}m generated for user {UserId}. MemoryId: {MemoryId}.",
                    months,
                    userId,
                    memory.Id);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "System memory generation failed for user {UserId}, period {Period}m.",
                    userId,
                    (int)period);
            }
        }
    }

    private async Task<List<SystemMemoryPhotoProjection>> LoadSourcePhotosAsync(
        Guid userId,
        SystemMemory? existingMemory,
        DateTime periodStart,
        DateTime periodEnd,
        bool devForceSystemMemories,
        CancellationToken ct)
    {
        IQueryable<Domain.Media.Photo> query = _db.Photos
            .AsNoTracking()
            .Where(x => x.UploadedByUserId == userId && !x.IsDeleted);

        if (!devForceSystemMemories)
        {
            var existingIds = existingMemory?.GetSourcePhotoIds() ?? Array.Empty<Guid>();

            if (existingIds.Count > 0)
            {
                query = query.Where(x => existingIds.Contains(x.Id));
            }
            else
            {
                query = query.Where(x => x.CreatedAt >= periodStart && x.CreatedAt <= periodEnd);
            }
        }

        return await query
            .OrderBy(x => x.CreatedAt)
            .Take(45)
            .Select(x => new SystemMemoryPhotoProjection(
                x.Id,
                x.StorageKey,
                x.ContentType,
                x.CreatedAt,
                x.Caption))
            .ToListAsync(ct);
    }

    private async Task<bool> HasSystemMemoryNotificationAsync(
        Guid userId,
        Guid memoryId,
        CancellationToken ct)
    {
        return await _db.Notifications
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.SystemMemoryId == memoryId, ct);
    }

    private async Task<SystemNotificationState> EnsureStateAsync(
        Guid userId,
        SystemNotificationState? current,
        CancellationToken ct)
    {
        if (current is not null)
            return current;

        current = SystemNotificationState.Create(userId);
        await _states.AddAsync(current, ct);
        return current;
    }

    private async Task PersistAndDispatchAsync(
        Notification notification,
        Guid userId,
        CancellationToken ct)
    {
        await _notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        var unreadCount = await _notifications.GetUnreadCountAsync(userId, ct);
        await _notificationRealtime.NotifyNotificationsChangedAsync(userId, unreadCount, ct);
        await _pushDelivery.TrySendAsync(notification, ct);
    }

    private sealed record SystemMemoryPhotoProjection(
        Guid Id,
        string StorageKey,
        string ContentType,
        DateTime CreatedAt,
        string? Caption);
}
