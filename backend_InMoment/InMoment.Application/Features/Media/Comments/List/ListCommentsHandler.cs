using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.List;

public sealed class ListCommentsHandler : IRequestHandler<ListCommentsQuery, IReadOnlyList<CommentDto>>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly ICommentRepository _comments;
    private readonly IUserRepository _users;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICommentReactionRepository _commentReactions;

    public ListCommentsHandler(
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

    public async Task<IReadOnlyList<CommentDto>> Handle(ListCommentsQuery q, CancellationToken ct)
    {
        if (q.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var limit = q.Limit is > 0 and <= 200 ? q.Limit : 50;

        var photo = await _photos.GetByIdAsync(q.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        var list = await _comments.GetByPhotoAsync(q.PhotoId, limit, ct);

        var visibleComments = new List<Comment>(list.Count);

        foreach (var item in list)
        {
            if (item.IsDeleted)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, item.UserId, ct))
                continue;

            visibleComments.Add(item);
        }

        if (visibleComments.Count == 0)
            return Array.Empty<CommentDto>();

        var parentMap = await LoadVisibleParentsAsync(visibleComments, ct);

        var userIds = visibleComments
            .Select(x => x.UserId)
            .Concat(parentMap.Values.Select(x => x.UserId))
            .Distinct()
            .ToList();

        var users = await _users.GetByIdsAsync(userIds, ct);
        var userMap = users.ToDictionary(x => x.Id, x => x);

        var commentIds = visibleComments.Select(x => x.Id).ToList();
        var reactionsSummaryMap = await _commentReactions.GetSummariesByCommentIdsAsync(commentIds, ct);
        var myReactionsMap = await _commentReactions.GetUserReactionsByCommentIdsAsync(commentIds, _current.UserId, ct);

        return visibleComments
            .Select(x => Map(x, userMap, parentMap, reactionsSummaryMap, myReactionsMap))
            .ToList();
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

    private CommentDto Map(
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
            .Select(kv => new CommentListReactionCountDto(kv.Key, kv.Value))
            .OrderByDescending(x => x.Count)
            .ToList();

        var reactionsCount = summaryForComment.Values.Sum();
        var myReaction = myReactionsMap.TryGetValue(comment.Id, out var myType)
            ? myType
            : ReactionType.None;

        return new CommentDto(
            Id: comment.Id,
            PhotoId: comment.PhotoId,
            UserId: comment.UserId,
            UserName: author?.UserName ?? string.Empty,
            FirstName: author?.FirstName ?? string.Empty,
            LastName: author?.LastName ?? string.Empty,
            ProfilePhotoUrl: author?.ProfilePhotoUrl,
            ParentCommentId: comment.ParentCommentId,
            ParentCommentUserId: parent?.UserId,
            ParentCommentUserName: parentAuthor?.UserName,
            ParentCommentTextPreview: parent is null ? null : BuildPreview(parent.Text),
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

    private static string BuildPreview(string text)
    {
        var normalized = (text ?? string.Empty).Trim();

        if (normalized.Length <= 80)
            return normalized;

        return normalized[..80];
    }
}