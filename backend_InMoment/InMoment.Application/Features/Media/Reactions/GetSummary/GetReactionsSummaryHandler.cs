using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.Reactions.GetSummary;

public sealed class GetReactionsSummaryHandler : IRequestHandler<GetReactionsSummaryQuery, ReactionsSummaryDto>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IReactionRepository _reactions;
    private readonly IBlockedUserRepository _blocks;

    public GetReactionsSummaryHandler(
        ICurrentUser current,
        IPhotoRepository photos,
        IGroupRepository groups,
        IReactionRepository reactions,
        IBlockedUserRepository blocks)
    {
        _current = current;
        _photos = photos;
        _groups = groups;
        _reactions = reactions;
        _blocks = blocks;
    }

    public async Task<ReactionsSummaryDto> Handle(GetReactionsSummaryQuery q, CancellationToken ct)
    {
        if (q.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var photo = await _photos.GetByIdAsync(q.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var summary = await _reactions.GetSummaryAsync(q.PhotoId, ct);
        var mine = await _reactions.GetByPhotoAndUserAsync(q.PhotoId, _current.UserId, ct);

        return new ReactionsSummaryDto(
            q.PhotoId,
            mine?.Type ?? ReactionType.None,
            summary.Select(kv => new ReactionCountDto(kv.Key, kv.Value)).ToList());
    }
}