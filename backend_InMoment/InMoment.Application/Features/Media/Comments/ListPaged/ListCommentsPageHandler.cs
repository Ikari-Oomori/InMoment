using System.Globalization;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.ListPaged;

public sealed class ListCommentsPageHandler : IRequestHandler<ListCommentsPageQuery, CommentsPageDto>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly ICommentRepository _comments;
    private readonly IUserRepository _users;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICommentReactionRepository _commentReactions;

    public ListCommentsPageHandler(
        ICurrentUser current,
        IPhotoRepository photos,
        IGroupRepository groups,
        ICommentRepository comments,
        IUserRepository users,
        IBlockedUserRepository blocks,
        ICommentReactionRepository commentReactions)
    {
        _current = current;
        _photos = photos;
        _groups = groups;
        _comments = comments;
        _users = users;
        _blocks = blocks;
        _commentReactions = commentReactions;
    }

    public async Task<CommentsPageDto> Handle(ListCommentsPageQuery q, CancellationToken ct)
    {
        if (q.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var limit = q.Limit is < 1 or > 50 ? 20 : q.Limit;

        var photo = await _photos.GetByIdAsync(q.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        DateTime? beforeCreatedAt = null;
        Guid? beforeCommentId = null;

        if (!string.IsNullOrWhiteSpace(q.Cursor))
        {
            if (!TryParseCursor(q.Cursor!, out beforeCreatedAt, out beforeCommentId))
                throw new ValidationException("Invalid cursor format.");
        }

        var rawItems = await _comments.GetPageByPhotoAsync(
            q.PhotoId,
            limit * 2,
            beforeCreatedAt,
            beforeCommentId,
            ct);

        if (rawItems.Count == 0)
            return new CommentsPageDto(Array.Empty<PagedCommentDto>(), null);

        var visibleItems = new List<Comment>(limit);

        foreach (var item in rawItems)
        {
            if (item.IsDeleted)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, item.UserId, ct))
                continue;

            visibleItems.Add(item);

            if (visibleItems.Count == limit)
                break;
        }

        if (visibleItems.Count == 0)
            return new CommentsPageDto(Array.Empty<PagedCommentDto>(), null);

        var parentMap = await LoadVisibleParentsAsync(visibleItems, ct);

        var userIds = visibleItems
            .Select(x => x.UserId)
            .Concat(parentMap.Values.Select(x => x.UserId))
            .Distinct()
            .ToList();

        var users = await _users.GetByIdsAsync(userIds, ct);
        var userMap = users.ToDictionary(x => x.Id, x => x);

        var commentIds = visibleItems.Select(x => x.Id).ToList();
        var reactionsSummaryMap = await _commentReactions.GetSummariesByCommentIdsAsync(commentIds, ct);
        var myReactionsMap = await _commentReactions.GetUserReactionsByCommentIdsAsync(commentIds, _current.UserId, ct);

        var dto = visibleItems
            .Select(x => Map(x, userMap, parentMap, reactionsSummaryMap, myReactionsMap))
            .ToList();

        var lastVisible = visibleItems[^1];

        var nextCursor = rawItems.Count < limit
            ? null
            : BuildCursor(lastVisible.CreatedAt, lastVisible.Id);

        return new CommentsPageDto(dto, nextCursor);
    }

    private async Task<Dictionary<Guid, Comment>> LoadVisibleParentsAsync(
        IReadOnlyList<Comment> comments,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, Comment>();

        var parentIds = comments
            .Where(x => x.ParentCommentId.HasValue)
            .Select(x => x.ParentCommentId!.Value)
            .Distinct()
            .ToList();

        foreach (var parentId in parentIds)
        {
            var parent = await _comments.GetByIdAsync(parentId, ct);
            if (parent is null || parent.IsDeleted)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, parent.UserId, ct))
                continue;

            result[parentId] = parent;
        }

        return result;
    }

    private PagedCommentDto Map(
        Comment comment,
        IReadOnlyDictionary<Guid, User> userMap,
        IReadOnlyDictionary<Guid, Comment> parentMap,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<ReactionType, int>> reactionsSummaryMap,
        IReadOnlyDictionary<Guid, ReactionType> myReactionsMap)
    {
        userMap.TryGetValue(comment.UserId, out var author);

        Comment? parent = null;
        User? parentAuthor = null;

        if (comment.ParentCommentId.HasValue &&
            parentMap.TryGetValue(comment.ParentCommentId.Value, out var loadedParent))
        {
            parent = loadedParent;
            userMap.TryGetValue(parent.UserId, out parentAuthor);
        }

        reactionsSummaryMap.TryGetValue(comment.Id, out var summaryForComment);
        summaryForComment ??= new Dictionary<ReactionType, int>();

        var reactions = summaryForComment
            .Select(kv => new PagedCommentReactionCountDto(kv.Key, kv.Value))
            .OrderByDescending(x => x.Count)
            .ToList();

        var reactionsCount = summaryForComment.Values.Sum();
        var myReaction = myReactionsMap.TryGetValue(comment.Id, out var myType)
            ? myType
            : ReactionType.None;

        var authorIsActive = author?.IsActive ?? false;
        var parentAuthorIsActive = parentAuthor?.IsActive;

        var authorUserName = authorIsActive ? (author?.UserName ?? string.Empty) : string.Empty;
        var authorFirstName = authorIsActive ? (author?.FirstName ?? string.Empty) : "Деактивированный";
        var authorLastName = authorIsActive ? (author?.LastName ?? string.Empty) : "пользователь";
        var authorProfilePhotoUrl = authorIsActive ? author?.ProfilePhotoUrl : null;

        var parentUserName = parentAuthorIsActive == true
            ? parentAuthor?.UserName
            : null;

        return new PagedCommentDto(
            Id: comment.Id,
            PhotoId: comment.PhotoId,
            UserId: comment.UserId,
            UserName: authorUserName,
            FirstName: authorFirstName,
            LastName: authorLastName,
            ProfilePhotoUrl: authorProfilePhotoUrl,
            UserIsActive: authorIsActive,
            ParentCommentId: comment.ParentCommentId,
            ParentCommentUserId: parent?.UserId,
            ParentCommentUserName: parentUserName,
            ParentCommentUserIsActive: parentAuthorIsActive,
            ParentCommentTextPreview: parent is null ? null : BuildPreview(parent.Text, parent.GifUrl),
            Text: comment.Text,
            GifUrl: comment.GifUrl,
            CreatedAt: comment.CreatedAt,
            EditedAt: comment.EditedAt,
            IsMine: comment.UserId == _current.UserId,
            Reactions: reactions,
            ReactionsCount: reactionsCount,
            MyReaction: myReaction
        );
    }

    private static string BuildPreview(string text, string? gifUrl = null)
    {
        var normalized = (text ?? string.Empty).Trim();

        if (normalized.Length == 0 && !string.IsNullOrWhiteSpace(gifUrl))
            return "GIF";

        if (normalized.Length <= 80)
            return normalized;

        return normalized[..80];
    }

    private static string BuildCursor(DateTime createdAt, Guid commentId)
        => $"{createdAt.ToUniversalTime():O}|{commentId:D}";

    private static bool TryParseCursor(string cursor, out DateTime? createdAt, out Guid? commentId)
    {
        createdAt = null;
        commentId = null;

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

        if (!Guid.TryParse(parts[1], out var parsedCommentId))
            return false;

        createdAt = parsedCreatedAt;
        commentId = parsedCommentId;
        return true;
    }
}