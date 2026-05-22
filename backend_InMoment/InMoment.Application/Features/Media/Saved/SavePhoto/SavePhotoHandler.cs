using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.Saved.SavePhoto;

public sealed class SavePhotoHandler : IRequestHandler<SavePhotoCommand>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly ISavedPhotoRepository _saved;
    private readonly IBlockedUserRepository _blocks;
    private readonly IUnitOfWork _uow;

    public SavePhotoHandler(
        ICurrentUser current,
        IPhotoRepository photos,
        IGroupRepository groups,
        ISavedPhotoRepository saved,
        IBlockedUserRepository blocks,
        IUnitOfWork uow)
    {
        _current = current;
        _photos = photos;
        _groups = groups;
        _saved = saved;
        _blocks = blocks;
        _uow = uow;
    }

    public async Task Handle(SavePhotoCommand cmd, CancellationToken ct)
    {
        if (cmd.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var photo = await _photos.GetByIdAsync(cmd.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var existing = await _saved.GetByPhotoAndUserAsync(cmd.PhotoId, _current.UserId, ct);
        if (existing is not null)
            return;

        var savedPhoto = SavedPhoto.Create(cmd.PhotoId, _current.UserId);

        await _saved.AddAsync(savedPhoto, ct);
        await _uow.SaveChangesAsync(ct);
    }
}