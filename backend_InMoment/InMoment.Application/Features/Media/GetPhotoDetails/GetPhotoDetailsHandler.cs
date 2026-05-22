using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using MediatR;

namespace InMoment.Application.Features.Media.GetPhotoDetails;

public sealed class GetPhotoDetailsHandler
    : IRequestHandler<GetPhotoDetailsQuery, PhotoDetailsDto>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly IReactionRepository _reactions;
    private readonly ICommentRepository _comments;
    private readonly IBlockedUserRepository _blocks;
    private readonly IFileStorage _storage;

    public GetPhotoDetailsHandler(
        ICurrentUser current,
        IPhotoRepository photos,
        IGroupRepository groups,
        IUserRepository users,
        IReactionRepository reactions,
        ICommentRepository comments,
        IBlockedUserRepository blocks,
        IFileStorage storage)
    {
        _current = current;
        _photos = photos;
        _groups = groups;
        _users = users;
        _reactions = reactions;
        _comments = comments;
        _blocks = blocks;
        _storage = storage;
    }

    public async Task<PhotoDetailsDto> Handle(GetPhotoDetailsQuery query, CancellationToken ct)
    {
        if (query.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var photo = await _photos.GetByIdAsync(query.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var author = await _users.GetByIdAsync(photo.UploadedByUserId, ct)
                     ?? throw new NotFoundException("Author not found.");

        var summary = await _reactions.GetSummaryAsync(photo.Id, ct);
        var myReaction = await _reactions.GetByPhotoAndUserAsync(photo.Id, _current.UserId, ct);
        var commentsCounts = await _comments.GetCountsByPhotoIdsAsync(new[] { photo.Id }, ct);

        var commentsCount = commentsCounts.TryGetValue(photo.Id, out var count)
            ? count
            : 0;

        var reactions = summary
            .Select(x => new PhotoReactionCountDto(x.Key, x.Value))
            .OrderByDescending(x => x.Count)
            .ToList();

        var isMine = photo.UploadedByUserId == _current.UserId;

        var group = await _groups.GetByIdAsync(photo.GroupId, ct);
        var canManageGroup = group?.IsManager(_current.UserId) ?? false;

        var authorIsActive = author.IsActive;

        var authorUserName = authorIsActive ? author.UserName : string.Empty;
        var authorFirstName = authorIsActive ? author.FirstName : "Деактивированный";
        var authorLastName = authorIsActive ? author.LastName : "пользователь";
        var authorProfilePhotoUrl = authorIsActive ? author.ProfilePhotoUrl : null;

        return new PhotoDetailsDto(
            PhotoId: photo.Id,
            GroupId: photo.GroupId,
            AuthorId: author.Id,
            AuthorUserName: authorUserName,
            AuthorFirstName: authorFirstName,
            AuthorLastName: authorLastName,
            AuthorProfilePhotoUrl: authorProfilePhotoUrl,
            AuthorIsActive: authorIsActive,
            Url: _storage.GetPublicUrl(photo.StorageKey),
            ContentType: photo.ContentType,
            SizeBytes: photo.SizeBytes,
            Caption: photo.Caption,
            CreatedAt: photo.CreatedAt,
            IsMine: isMine,
            CanEdit: isMine,
            CanDelete: isMine || canManageGroup,
            MyReaction: myReaction?.Type ?? ReactionType.None,
            Reactions: reactions,
            CommentsCount: commentsCount
        );
    }
}