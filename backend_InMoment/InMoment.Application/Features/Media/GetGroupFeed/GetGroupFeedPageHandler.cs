using System.Globalization;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.GetGroupFeed;

public sealed class GetGroupFeedPageHandler : IRequestHandler<GetGroupFeedPageQuery, FeedPageDto>
{
    private readonly ICurrentUser _current;
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly IUserRepository _users;
    private readonly IFileStorage _storage;
    private readonly IReactionRepository _reactions;
    private readonly ICommentRepository _comments;

    public GetGroupFeedPageHandler(
        ICurrentUser current,
        IGroupRepository groups,
        IPhotoRepository photos,
        IUserRepository users,
        IFileStorage storage,
        IReactionRepository reactions,
        ICommentRepository comments)
    {
        _current = current;
        _groups = groups;
        _photos = photos;
        _users = users;
        _storage = storage;
        _reactions = reactions;
        _comments = comments;
    }

    public async Task<FeedPageDto> Handle(GetGroupFeedPageQuery q, CancellationToken ct)
    {
        if (q.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var isMember = await _groups.IsMemberAsync(q.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        var limit = q.Limit is < 1 or > 50 ? 20 : q.Limit;

        DateTime? beforeCreatedAt = null;
        Guid? beforePhotoId = null;

        if (!string.IsNullOrWhiteSpace(q.Cursor))
        {
            if (!TryParseCursor(q.Cursor!, out beforeCreatedAt, out beforePhotoId))
                throw new ValidationException("Invalid cursor format.");
        }

        var items = await _photos.GetPageByGroupAsync(
            q.GroupId,
            limit,
            beforeCreatedAt,
            beforePhotoId,
            ct);

        if (items.Count == 0)
            return new FeedPageDto(Array.Empty<GroupFeedItemDto>(), null);

        var photoIds = items.Select(x => x.Id).ToList();
        var authorIds = items
            .Select(x => x.UploadedByUserId)
            .Distinct()
            .ToList();

        var authors = await _users.GetByIdsAsync(authorIds, ct);
        var authorMap = authors.ToDictionary(x => x.Id, x => x);

        var reactionsSummary = await _reactions.GetSummariesByPhotoIdsAsync(photoIds, ct);
        var myReactions = await _reactions.GetUserReactionsByPhotoIdsAsync(photoIds, _current.UserId, ct);
        var commentsCounts = await _comments.GetCountsByPhotoIdsAsync(photoIds, ct);

        var dto = items.Select(p =>
        {
            reactionsSummary.TryGetValue(p.Id, out var summaryForPhoto);
            summaryForPhoto ??= new Dictionary<ReactionType, int>();

            authorMap.TryGetValue(p.UploadedByUserId, out var author);

            var reactionsList = summaryForPhoto
                .Select(kv => new FeedReactionCountDto(kv.Key, kv.Value))
                .OrderByDescending(x => x.Count)
                .ToList();

            var reactionsCount = summaryForPhoto.Values.Sum();

            var my = myReactions.TryGetValue(p.Id, out var myType) ? myType : ReactionType.None;
            var commentsCount = commentsCounts.TryGetValue(p.Id, out var count) ? count : 0;

            return new GroupFeedItemDto(
                p.Id,
                p.GroupId,
                p.UploadedByUserId,
                author?.UserName ?? string.Empty,
                author?.ProfilePhotoUrl,
                _storage.GetPublicUrl(p.StorageKey),
                p.ContentType,
                p.SizeBytes,
                p.Caption,
                p.CreatedAt,
                reactionsList,
                reactionsCount,
                my,
                commentsCount);
        }).ToList();

        string? nextCursor = null;

        if (items.Count == limit)
        {
            var last = items[^1];
            nextCursor = BuildCursor(last.CreatedAt, last.Id);
        }

        return new FeedPageDto(dto, nextCursor);
    }

    private static string BuildCursor(DateTime createdAt, Guid photoId)
        => $"{createdAt.ToUniversalTime():O}|{photoId:D}";

    private static bool TryParseCursor(string cursor, out DateTime? createdAt, out Guid? photoId)
    {
        createdAt = null;
        photoId = null;

        var parts = cursor.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!DateTime.TryParse(
                parts[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedCreatedAt))
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out var parsedPhotoId))
            return false;

        createdAt = parsedCreatedAt;
        photoId = parsedPhotoId;
        return true;
    }
}