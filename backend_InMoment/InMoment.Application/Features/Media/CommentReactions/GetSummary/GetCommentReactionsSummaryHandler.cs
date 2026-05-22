using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.CommentReactions.GetSummary;

public sealed class GetCommentReactionsSummaryHandler
    : IRequestHandler<GetCommentReactionsSummaryQuery, CommentReactionsSummaryDto>
{
    private readonly ICurrentUser _current;
    private readonly ICommentRepository _comments;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly ICommentReactionRepository _commentReactions;
    private readonly IBlockedUserRepository _blocks;

    public GetCommentReactionsSummaryHandler(
        ICurrentUser current,
        ICommentRepository comments,
        IPhotoRepository photos,
        IGroupRepository groups,
        ICommentReactionRepository commentReactions,
        IBlockedUserRepository blocks)
    {
        _current = current;
        _comments = comments;
        _photos = photos;
        _groups = groups;
        _commentReactions = commentReactions;
        _blocks = blocks;
    }

    public async Task<CommentReactionsSummaryDto> Handle(
        GetCommentReactionsSummaryQuery q,
        CancellationToken ct)
    {
        if (q.CommentId == Guid.Empty)
            throw new ValidationException("CommentId is required.");

        var comment = await _comments.GetByIdAsync(q.CommentId, ct)
                      ?? throw new NotFoundException("Comment not found.");

        if (comment.IsDeleted)
            throw new NotFoundException("Comment not found.");

        var photo = await _photos.GetByIdAsync(comment.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, comment.UserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var summary = await _commentReactions.GetSummaryAsync(q.CommentId, ct);
        var mine = await _commentReactions.GetByCommentAndUserAsync(q.CommentId, _current.UserId, ct);

        return new CommentReactionsSummaryDto(
            q.CommentId,
            mine?.Type ?? ReactionType.None,
            summary.Select(kv => new CommentReactionCountDto(kv.Key, kv.Value)).ToList());
    }
}