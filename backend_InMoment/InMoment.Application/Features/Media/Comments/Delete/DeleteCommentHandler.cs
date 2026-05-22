using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.Delete;

public sealed class DeleteCommentHandler : IRequestHandler<DeleteCommentCommand>
{
    private readonly ICurrentUser _current;
    private readonly ICommentRepository _comments;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public DeleteCommentHandler(
        ICurrentUser current,
        ICommentRepository comments,
        IPhotoRepository photos,
        IGroupRepository groups,
        IUnitOfWork uow,
        IGroupRealtime realtime)
    {
        _current = current;
        _comments = comments;
        _photos = photos;
        _groups = groups;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task Handle(DeleteCommentCommand cmd, CancellationToken ct)
    {
        var comment = await _comments.GetByIdAsync(cmd.CommentId, ct)
                      ?? throw new NotFoundException("Comment not found.");

        var photo = await _photos.GetByIdAsync(comment.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (comment.UserId == _current.UserId)
        {
            comment.Delete(_current.UserId);
            await _uow.SaveChangesAsync(ct);

            await _realtime.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", photo.Id, ct);
            return;
        }

        var group = await _groups.GetByIdAsync(photo.GroupId, ct)
                    ?? throw new NotFoundException("Group not found.");

        if (group.OwnerId != _current.UserId)
            throw new ForbiddenException("You cannot delete this comment.");

        comment.DeleteAsOwner(_current.UserId);

        await _uow.SaveChangesAsync(ct);
        await _realtime.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", photo.Id, ct);
    }
}