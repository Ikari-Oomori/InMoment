using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Media.EditPhotoCaption;

public sealed class EditPhotoCaptionHandler : IRequestHandler<EditPhotoCaptionCommand, Guid>
{
    private readonly ICurrentUser _current;
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public EditPhotoCaptionHandler(
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

    public async Task<Guid> Handle(EditPhotoCaptionCommand cmd, CancellationToken ct)
    {
        if (cmd.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        if (cmd.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var isMember = await _groups.IsMemberAsync(cmd.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        var photo = await _photos.GetByIdAsync(cmd.PhotoId, ct)
                    ?? throw new NotFoundException("Photo not found.");

        if (photo.GroupId != cmd.GroupId)
            throw new ValidationException("Photo does not belong to this group.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        photo.EditCaption(_current.UserId, cmd.Caption);

        await _uow.SaveChangesAsync(ct);

        await _realtime.NotifyFeedChangedAsync(
            cmd.GroupId,
            reason: "photo_updated",
            photoId: cmd.PhotoId,
            ct);

        return photo.Id;
    }
}