using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Contacts.Invites.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Contacts;
using MediatR;

namespace InMoment.Application.Features.Contacts.Invites.Send;

public sealed class SendContactInviteHandler
    : IRequestHandler<SendContactInviteCommand, ContactInviteDto>
{
    private readonly IContactInviteRepository _invites;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;
    private readonly IContactInviteSender _sender;
    private readonly IUnitOfWork _uow;

    public SendContactInviteHandler(
        IContactInviteRepository invites,
        IUserRepository users,
        ICurrentUser current,
        IContactInviteSender sender,
        IUnitOfWork uow)
    {
        _invites = invites;
        _users = users;
        _current = current;
        _sender = sender;
        _uow = uow;
    }

    public async Task<ContactInviteDto> Handle(SendContactInviteCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var hasEmail = !string.IsNullOrWhiteSpace(request.Email);
        var hasPhone = !string.IsNullOrWhiteSpace(request.PhoneNumber);

        if (hasEmail == hasPhone)
            throw new ValidationException("Exactly one contact target must be provided.");

        ContactInvite invite;

        if (hasEmail)
        {
            var email = request.Email!.Trim().ToLowerInvariant();

            var existingUser = await _users.GetByEmailAsync(email, ct);
            if (existingUser is not null && existingUser.IsActive)
                throw new ValidationException("Этот контакт уже зарегистрирован в системе.");

            var existingPending = await _invites.GetPendingByEmailAsync(_current.UserId, email, ct);
            if (existingPending is not null)
                throw new ValidationException("Приглашение для этого email уже отправлено.");

            invite = ContactInvite.CreateEmail(
                _current.UserId,
                email,
                request.DisplayName,
                CreateToken());
        }
        else
        {
            var normalizedPhone = PhoneNumberNormalizer.Normalize(request.PhoneNumber)
                                  ?? throw new ValidationException("PhoneNumber is required.");

            var existingUser = await _users.GetByPhoneNumberAsync(normalizedPhone, ct);
            if (existingUser is not null && existingUser.IsActive)
                throw new ValidationException("Этот контакт уже зарегистрирован в системе.");

            var existingPending = await _invites.GetPendingByPhoneAsync(_current.UserId, normalizedPhone, ct);
            if (existingPending is not null)
                throw new ValidationException("Приглашение для этого номера уже отправлено.");

            invite = ContactInvite.CreateSms(
                _current.UserId,
                normalizedPhone,
                request.DisplayName,
                CreateToken());
        }

        await _invites.AddAsync(invite, ct);
        await _uow.SaveChangesAsync(ct);

        var inviteLink = $"inmoment://invite/contact/{invite.InviteToken}";

        await _sender.SendAsync(
            new ContactInviteSendRequest(
                invite.Channel,
                invite.Email,
                invite.PhoneNumber,
                invite.DisplayName,
                inviteLink),
            ct);

        return new ContactInviteDto(
            invite.Id,
            invite.Channel,
            invite.Email,
            invite.PhoneNumber,
            invite.DisplayName,
            invite.InviteToken,
            invite.Status,
            invite.CreatedAtUtc,
            invite.CancelledAtUtc);
    }

    private static string CreateToken()
        => Guid.NewGuid().ToString("N");
}