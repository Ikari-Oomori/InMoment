using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.Edit;

public sealed class EditCommentHandler : IRequestHandler<EditCommentCommand, Guid>
{
    private readonly ICommentRepository _comments;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public EditCommentHandler(
        ICommentRepository comments,
        IPhotoRepository photos,
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _comments = comments;
        _photos = photos;
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Guid> Handle(EditCommentCommand cmd, CancellationToken ct)
    {
        if (cmd.CommentId == Guid.Empty)
            throw new ValidationException("CommentId is required.");

        var comment = await _comments.GetByIdAsync(cmd.CommentId, ct)
                      ?? throw new NotFoundException("Comment not found.");

        var photo = await _photos.GetByIdAsync(comment.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        comment.Edit(_current.UserId, cmd.Text);

        await _uow.SaveChangesAsync(ct);
        return comment.Id;
    }
}