using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Users.UpdateMe;

public sealed class UpdateMeHandler : IRequestHandler<UpdateMeCommand, UpdatedMeDto>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public UpdateMeHandler(IUserRepository users, IUnitOfWork uow, ICurrentUser current)
    {
        _users = users;
        _uow = uow;
        _current = current;
    }

    public async Task<UpdatedMeDto> Handle(UpdateMeCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(cmd.UserName))
        {
            var newName = cmd.UserName.Trim();

            if (!string.Equals(newName, user.UserName, StringComparison.Ordinal))
            {
                if (await _users.UserNameExistsAsync(newName, ct))
                    throw new ValidationException("Nickname is already used.");

                user.ChangeUserName(newName);
            }
        }

        if (!string.IsNullOrWhiteSpace(cmd.FirstName) || !string.IsNullOrWhiteSpace(cmd.LastName))
        {
            var first = string.IsNullOrWhiteSpace(cmd.FirstName) ? user.FirstName : cmd.FirstName.Trim();
            var last = string.IsNullOrWhiteSpace(cmd.LastName) ? user.LastName : cmd.LastName.Trim();
            user.ChangeName(first, last);
        }

        if (cmd.PhoneNumber is not null)
        {
            var currentPhoneNumber = PhoneNumberNormalizer.Normalize(user.PhoneNumber);
            var newPhoneNumber = PhoneNumberNormalizer.Normalize(cmd.PhoneNumber);

            if (!string.Equals(currentPhoneNumber, newPhoneNumber, StringComparison.Ordinal))
            {
                if (newPhoneNumber is not null && await _users.PhoneNumberExistsAsync(newPhoneNumber, ct))
                    throw new ValidationException("Phone number is already used.");

                user.SetPhoneNumber(newPhoneNumber);
            }
        }

        await _uow.SaveChangesAsync(ct);

        return new UpdatedMeDto(
            user.Id,
            user.Email,
            user.UserName,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.ProfilePhotoUrl,
            user.ActiveGroupId,
            user.CreatedAt
        );
    }

}