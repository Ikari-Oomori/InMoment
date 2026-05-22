using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Media.DeletePhoto;

public sealed class DeletePhotoHandler : IRequestHandler<DeletePhotoCommand>
{
    private readonly ICurrentUser _current;
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public DeletePhotoHandler(
        ICurrentUser current,
        IGroupRepository groups,
        IPhotoRepository photos,
        IUnitOfWork uow,
        IGroupRealtime realtime)
    {
        _current = current;
        _groups = groups;
        _photos = photos;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task Handle(DeletePhotoCommand cmd, CancellationToken ct)
    {
        if (cmd.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        if (cmd.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var isMember = await _groups.IsMemberAsync(cmd.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                    ?? throw new NotFoundException("Group not found.");

        var photo = await _photos.GetByIdAsync(cmd.PhotoId, ct)
                    ?? throw new NotFoundException("Photo not found.");

        if (photo.GroupId != cmd.GroupId)
            throw new ValidationException("Photo does not belong to this group.");

        if (photo.IsDeleted)
            return;

        var canManageGroup = group.IsManager(_current.UserId);
        photo.MarkDeleted(_current.UserId, canManageGroup);

        await _uow.SaveChangesAsync(ct);

        await _realtime.NotifyFeedChangedAsync(
            cmd.GroupId,
            reason: "photo_deleted",
            photoId: cmd.PhotoId,
            ct);
    }
}