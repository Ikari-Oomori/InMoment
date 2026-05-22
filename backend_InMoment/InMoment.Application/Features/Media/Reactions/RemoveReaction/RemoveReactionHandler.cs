using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Reactions.RemoveReaction;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Media.Reactions.Remove;

public sealed class RemoveReactionHandler : IRequestHandler<RemoveReactionCommand>
{
    private readonly ICurrentUser _current;
    private readonly IReactionRepository _reactions;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IBlockedUserRepository _blocks;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public RemoveReactionHandler(
        ICurrentUser current,
        IReactionRepository reactions,
        IPhotoRepository photos,
        IGroupRepository groups,
        IBlockedUserRepository blocks,
        IUnitOfWork uow,
        IGroupRealtime realtime)
    {
        _current = current;
        _reactions = reactions;
        _photos = photos;
        _groups = groups;
        _blocks = blocks;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task Handle(RemoveReactionCommand cmd, CancellationToken ct)
    {
        var photo = await _photos.GetByIdAsync(cmd.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var existing = await _reactions.GetByPhotoAndUserAsync(cmd.PhotoId, _current.UserId, ct);
        if (existing is null)
            return;

        await _reactions.RemoveAsync(existing, ct);
        await _uow.SaveChangesAsync(ct);

        await _realtime.NotifyFeedChangedAsync(photo.GroupId, "reaction_changed", cmd.PhotoId, ct);
    }
}