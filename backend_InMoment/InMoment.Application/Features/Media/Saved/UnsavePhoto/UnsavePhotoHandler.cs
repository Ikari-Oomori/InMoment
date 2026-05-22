using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Media.Saved.UnsavePhoto;

public sealed class UnsavePhotoHandler : IRequestHandler<UnsavePhotoCommand>
{
    private readonly ICurrentUser _current;
    private readonly ISavedPhotoRepository _saved;
    private readonly IUnitOfWork _uow;

    public UnsavePhotoHandler(
        ICurrentUser current,
        ISavedPhotoRepository saved,
        IUnitOfWork uow)
    {
        _current = current;
        _saved = saved;
        _uow = uow;
    }

    public async Task Handle(UnsavePhotoCommand cmd, CancellationToken ct)
    {
        if (cmd.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var existing = await _saved.GetByPhotoAndUserAsync(cmd.PhotoId, _current.UserId, ct);
        if (existing is null)
            return;

        await _saved.RemoveAsync(existing, ct);
        await _uow.SaveChangesAsync(ct);
    }
}