using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Users.SetProfilePhoto;

public sealed class SetProfilePhotoHandler : IRequestHandler<SetProfilePhotoCommand>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public SetProfilePhotoHandler(IUserRepository users, IUnitOfWork uow, ICurrentUser current)
    {
        _users = users;
        _uow = uow;
        _current = current;
    }

    public async Task Handle(SetProfilePhotoCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(cmd.Url) && !IsValidAbsoluteHttpUrl(cmd.Url))
            throw new ValidationException("Profile photo url is invalid.");

        user.SetProfilePhoto(cmd.Url);
        await _uow.SaveChangesAsync(ct);
    }

    private static bool IsValidAbsoluteHttpUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }
}