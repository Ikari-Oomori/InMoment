using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Discussions.ListGroupDiscussions;

public sealed class ListGroupDiscussionsHandler
    : IRequestHandler<ListGroupDiscussionsQuery, IReadOnlyList<GroupDiscussionDto>>
{
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly ICommentRepository _comments;
    private readonly IUserRepository _users;
    private readonly IFileStorage _storage;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICurrentUser _current;
    private readonly IReactionRepository _reactions;

    public ListGroupDiscussionsHandler(
        IGroupRepository groups,
        IPhotoRepository photos,
        ICommentRepository comments,
        IUserRepository users,
        IFileStorage storage,
        IBlockedUserRepository blocks,
        ICurrentUser current,
        IReactionRepository reactions)
    {
        _groups = groups;
        _photos = photos;
        _comments = comments;
        _users = users;
        _storage = storage;
        _blocks = blocks;
        _current = current;
        _reactions = reactions;
    }

    public async Task<IReadOnlyList<GroupDiscussionDto>> Handle(
        ListGroupDiscussionsQuery q,
        CancellationToken ct)
    {
        if (q.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var group = await _groups.GetByIdAsync(q.GroupId, ct)
                    ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);

        var limit = q.Limit is < 1 or > 100 ? 30 : q.Limit;

        var photos = await _photos.GetFeedByGroupAsync(q.GroupId, limit, ct);
        if (photos.Count == 0)
            return Array.Empty<GroupDiscussionDto>();

        var photoIds = photos.Select(x => x.Id).ToList();

        var photoAuthorIds = photos
            .Select(x => x.UploadedByUserId)
            .Distinct()
            .ToList();

        var commentsCountMap = await _comments.GetCountsByPhotoIdsAsync(photoIds, ct);
        var latestCommentsMap = await _comments.GetLatestByPhotoIdsAsync(photoIds, ct);
        var reactionsSummaryMap = await _reactions.GetSummariesByPhotoIdsAsync(photoIds, ct);
        var myReactionsMap = await _reactions.GetUserReactionsByPhotoIdsAsync(photoIds, _current.UserId, ct);

        var latestCommentAuthorIds = latestCommentsMap.Values
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var allUserIds = photoAuthorIds
            .Concat(latestCommentAuthorIds)
            .Distinct()
            .ToList();

        var users = await _users.GetByIdsAsync(allUserIds, ct);
        var userMap = users.ToDictionary(x => x.Id, x => x);

        var result = new List<GroupDiscussionDto>(photos.Count);

        foreach (var photo in photos)
        {
            commentsCountMap.TryGetValue(photo.Id, out var count);
            latestCommentsMap.TryGetValue(photo.Id, out var latest);

            reactionsSummaryMap.TryGetValue(photo.Id, out var summaryForPhoto);
            summaryForPhoto ??= new Dictionary<ReactionType, int>();

            var reactions = summaryForPhoto
                .Select(kv => new DiscussionReactionCountDto(kv.Key, kv.Value))
                .OrderByDescending(x => x.Count)
                .ToList();

            var reactionsCount = summaryForPhoto.Values.Sum();
            var myReaction = myReactionsMap.TryGetValue(photo.Id, out var myType)
                ? myType
                : ReactionType.None;

            string? latestCommentText = latest?.Text;
            Guid? latestCommentUserId = latest?.UserId;
            string? latestCommentUserName = null;
            string? latestCommentUserProfilePhotoUrl = null;
            DateTime? latestCommentCreatedAt = latest?.CreatedAt;

            if (latest is not null &&
                await _blocks.ExistsEitherDirectionAsync(_current.UserId, latest.UserId, ct))
            {
                latestCommentText = null;
                latestCommentUserId = null;
                latestCommentUserName = null;
                latestCommentUserProfilePhotoUrl = null;
                latestCommentCreatedAt = null;
            }
            else if (latest is not null &&
                     userMap.TryGetValue(latest.UserId, out var latestCommentAuthor))
            {
                latestCommentUserName = latestCommentAuthor.UserName;
                latestCommentUserProfilePhotoUrl = latestCommentAuthor.ProfilePhotoUrl;
            }

            userMap.TryGetValue(photo.UploadedByUserId, out var author);

            var lastActivityAt = latestCommentCreatedAt ?? photo.CreatedAt;

            result.Add(new GroupDiscussionDto(
                PhotoId: photo.Id,
                PhotoUrl: _storage.GetPublicUrl(photo.StorageKey),
                PhotoCreatedAt: photo.CreatedAt,
                PhotoCaption: photo.Caption,
                PhotoAuthorUserId: photo.UploadedByUserId,
                PhotoAuthorUserName: author?.UserName ?? string.Empty,
                PhotoAuthorProfilePhotoUrl: author?.ProfilePhotoUrl,
                Reactions: reactions,
                ReactionsCount: reactionsCount,
                MyReaction: myReaction,
                CommentsCount: count,
                LatestCommentText: latestCommentText,
                LatestCommentUserId: latestCommentUserId,
                LatestCommentUserName: latestCommentUserName,
                LatestCommentUserProfilePhotoUrl: latestCommentUserProfilePhotoUrl,
                LatestCommentCreatedAt: latestCommentCreatedAt,
                LastActivityAt: lastActivityAt));
        }

        return result;
    }
}