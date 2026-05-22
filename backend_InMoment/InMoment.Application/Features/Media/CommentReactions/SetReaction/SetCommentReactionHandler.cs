using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.CommentReactions.SetReaction;

public sealed class SetCommentReactionHandler : IRequestHandler<SetCommentReactionCommand>
{
    private readonly ICurrentUser _current;
    private readonly ICommentRepository _comments;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly ICommentReactionRepository _commentReactions;
    private readonly IBlockedUserRepository _blocks;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public SetCommentReactionHandler(
        ICurrentUser current,
        ICommentRepository comments,
        IPhotoRepository photos,
        IGroupRepository groups,
        ICommentReactionRepository commentReactions,
        IBlockedUserRepository blocks,
        IUnitOfWork uow,
        IGroupRealtime realtime)
    {
        _current = current;
        _comments = comments;
        _photos = photos;
        _groups = groups;
        _commentReactions = commentReactions;
        _blocks = blocks;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task Handle(SetCommentReactionCommand cmd, CancellationToken ct)
    {
        if (cmd.CommentId == Guid.Empty)
            throw new ValidationException("CommentId is required.");

        if (cmd.Type == ReactionType.None)
            throw new ValidationException("ReactionType is required.");

        var comment = await _comments.GetByIdAsync(cmd.CommentId, ct)
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

        var existing = await _commentReactions.GetByCommentAndUserAsync(cmd.CommentId, _current.UserId, ct);

        if (existing is null)
        {
            var reaction = CommentReaction.Create(cmd.CommentId, _current.UserId, cmd.Type);
            await _commentReactions.AddAsync(reaction, ct);
        }
        else
        {
            existing.Change(cmd.Type);
        }

        await _uow.SaveChangesAsync(ct);
        await _realtime.NotifyFeedChangedAsync(photo.GroupId, "comment_reaction_changed", photo.Id, ct);
    }
}