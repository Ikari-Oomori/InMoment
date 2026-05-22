using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Groups;
using InMoment.Domain.Notifications;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Accounts;

public sealed class AccountDataManager : IAccountDataManager
{
    private readonly AppDbContext _db;

    public AccountDataManager(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AccountDataSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
                   ?? throw new NotFoundException("Пользователь не найден.");

        var groupsCount = await _db.GroupMembers
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.IsActive, ct);

        var ownedGroupsCount = await _db.Groups
            .AsNoTracking()
            .CountAsync(x => x.OwnerId == userId && x.IsActive, ct);

        var photosCount = await _db.Photos
            .AsNoTracking()
            .CountAsync(x => x.UploadedByUserId == userId && !x.IsDeleted, ct);

        var commentsCount = await _db.Set<Domain.Media.Comment>()
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        var reactionsCount = await _db.Reactions
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, ct);

        var friendshipsCount = await _db.Set<Friendship>()
            .AsNoTracking()
            .CountAsync(x => x.User1Id == userId || x.User2Id == userId, ct);

        var activeSessionsCount = await _db.Set<RefreshSession>()
            .AsNoTracking()
            .CountAsync(x =>
                x.UserId == userId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > DateTime.UtcNow, ct);

        return new AccountDataSummaryDto(
            UserId: userId,
            IsActive: user.IsActive,
            GroupsCount: groupsCount,
            OwnedGroupsCount: ownedGroupsCount,
            PhotosCount: photosCount,
            CommentsCount: commentsCount,
            ReactionsCount: reactionsCount,
            FriendshipsCount: friendshipsCount,
            ActiveSessionsCount: activeSessionsCount
        );
    }

    public async Task<AccountDeletionRequestDto?> GetLatestDeletionRequestAsync(Guid userId, CancellationToken ct)
    {
        var entity = await _db.AccountDeletionRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : Map(entity);
    }

    public async Task<AccountDeletionRequestDto> CreateDeletionRequestAsync(Guid userId, string? note, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
                   ?? throw new NotFoundException("Пользователь не найден.");

        var hasActiveRequest = await _db.AccountDeletionRequests
            .AnyAsync(x =>
                x.UserId == userId &&
                (x.Status == AccountDeletionRequestStatus.Pending ||
                 x.Status == AccountDeletionRequestStatus.InProgress), ct);

        if (hasActiveRequest)
            throw new ValidationException("У вас уже есть активный запрос на удаление аккаунта и данных.");

        var entity = AccountDeletionRequest.Create(
            userId: user.Id,
            requestedEmail: user.Email,
            requestedUserName: user.UserName,
            note: note);

        _db.AccountDeletionRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<IReadOnlyList<AccountDeletionRequestDto>> ListDeletionRequestsAsync(
    int limit,
    AccountDeletionRequestStatus? status,
    CancellationToken ct)
    {
        var query = _db.AccountDeletionRequests
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedAtUtc)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var items = await query
            .Take(limit)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<AccountDeletionRequestDto> GetDeletionRequestByIdAsync(Guid requestId, CancellationToken ct)
    {
        var entity = await _db.AccountDeletionRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException("Запрос на удаление аккаунта не найден.");

        return Map(entity);
    }

    public async Task<AccountDeletionRequestDto> ReviewDeletionRequestAsync(
        Guid requestId,
        Guid moderatorUserId,
        AccountDeletionRequestStatus status,
        string? processingNote,
        bool permanentlyDeleteNow,
        CancellationToken ct)
    {
        var entity = await _db.AccountDeletionRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException("Запрос на удаление аккаунта не найден.");

        switch (status)
        {
            case AccountDeletionRequestStatus.InProgress:
                entity.MarkInProgress(moderatorUserId, processingNote);
                await _db.SaveChangesAsync(ct);
                return Map(entity);

            case AccountDeletionRequestStatus.Rejected:
                entity.Reject(moderatorUserId, processingNote);
                await _db.SaveChangesAsync(ct);
                return Map(entity);

            case AccountDeletionRequestStatus.Completed:
                if (!permanentlyDeleteNow)
                    throw new ValidationException(
                        "Для завершения запроса необходимо выполнить фактическое удаление аккаунта.");

                entity.Complete(moderatorUserId, processingNote);
                await _db.SaveChangesAsync(ct);

                await PermanentlyDeleteAccountAsync(entity.UserId, ct);

                return Map(entity);

            default:
                throw new ValidationException(
                    "Для модератора доступны только статусы InProgress, Rejected и Completed.");
        }
    }

    public async Task DeactivateAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new NotFoundException("Пользователь не найден.");

        if (!user.IsActive)
            return;

        await DetachFromGroupsAsync(userId, ct);
        await RevokeSessionsAsync(userId, ct);
        await MarkNotificationsReadAsync(userId, ct);

        user.Deactivate();

        await _db.SaveChangesAsync(ct);
    }

    public async Task PermanentlyDeleteAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
                   ?? throw new NotFoundException("Пользователь не найден.");

        await DetachFromGroupsAsync(userId, ct);
        await RevokeSessionsAsync(userId, ct);
        await MarkNotificationsReadAsync(userId, ct);

        var refreshSessions = await _db.Set<RefreshSession>()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var passwordResetTokens = await _db.Set<PasswordResetToken>()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var notifications = await _db.Notifications
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var deviceTokens = await _db.DeviceTokens
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var reactions = await _db.Reactions
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var commentReactions = await _db.CommentReactions
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var savedPhotos = await _db.SavedPhotos
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var friendRequests = await _db.Set<FriendRequest>()
            .Where(x => x.FromUserId == userId || x.ToUserId == userId)
            .ToListAsync(ct);

        var friendships = await _db.Set<Friendship>()
            .Where(x => x.User1Id == userId || x.User2Id == userId)
            .ToListAsync(ct);

        var privacySettings = await _db.PrivacySettings
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var blockedUsers = await _db.BlockedUsers
            .Where(x => x.UserId == userId || x.BlockedUserId == userId)
            .ToListAsync(ct);

        _db.Set<RefreshSession>().RemoveRange(refreshSessions);
        _db.Set<PasswordResetToken>().RemoveRange(passwordResetTokens);
        _db.Notifications.RemoveRange(notifications);
        _db.DeviceTokens.RemoveRange(deviceTokens);
        _db.Reactions.RemoveRange(reactions);
        _db.CommentReactions.RemoveRange(commentReactions);
        _db.SavedPhotos.RemoveRange(savedPhotos);
        _db.Set<FriendRequest>().RemoveRange(friendRequests);
        _db.Set<Friendship>().RemoveRange(friendships);
        _db.PrivacySettings.RemoveRange(privacySettings);
        _db.BlockedUsers.RemoveRange(blockedUsers);

        user.PermanentlyDelete();

        await _db.SaveChangesAsync(ct);
    }

    private async Task DetachFromGroupsAsync(Guid userId, CancellationToken ct)
    {
        var groups = await _db.Groups
            .Include(x => x.Members)
            .Where(g => g.IsActive && g.Members.Any(m => m.UserId == userId && m.IsActive))
            .ToListAsync(ct);

        foreach (var group in groups)
        {
            if (group.OwnerId == userId)
            {
                var replacementOwner = group.Members
                    .Where(m => m.IsActive && m.UserId != userId)
                    .OrderBy(m => m.CreatedAt)
                    .FirstOrDefault();

                if (replacementOwner is not null)
                    group.TransferOwnership(userId, replacementOwner.UserId);
            }

            if (group.IsMember(userId))
                group.Leave(userId);
        }
    }

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var sessions = await _db.Set<RefreshSession>()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        foreach (var session in sessions)
            session.Revoke("account_state_changed", DateTime.UtcNow);
    }

    private async Task MarkNotificationsReadAsync(Guid userId, CancellationToken ct)
    {
        var notifications = await _db.Set<Notification>()
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(ct);

        foreach (var notification in notifications)
            notification.MarkRead();
    }

    private static AccountDeletionRequestDto Map(AccountDeletionRequest entity)
    {
        return new AccountDeletionRequestDto(
            Id: entity.Id,
            UserId: entity.UserId,
            StatusCode: (int)entity.Status,
            Status: entity.Status.ToString(),
            RequestedEmail: entity.RequestedEmail,
            RequestedUserName: entity.RequestedUserName,
            Note: entity.Note,
            ProcessingNote: entity.ProcessingNote,
            ProcessedByUserId: entity.ProcessedByUserId,
            RequestedAtUtc: entity.RequestedAtUtc,
            UpdatedAtUtc: entity.UpdatedAtUtc,
            ProcessedAtUtc: entity.ProcessedAtUtc
        );
    }
}