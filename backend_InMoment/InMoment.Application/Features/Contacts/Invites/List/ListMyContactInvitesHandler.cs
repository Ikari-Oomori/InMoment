using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Contacts.Invites.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Contacts.Invites.List;

public sealed class ListMyContactInvitesHandler
    : IRequestHandler<ListMyContactInvitesQuery, IReadOnlyList<ContactInviteDto>>
{
    private readonly IContactInviteRepository _invites;
    private readonly ICurrentUser _current;

    public ListMyContactInvitesHandler(
        IContactInviteRepository invites,
        ICurrentUser current)
    {
        _invites = invites;
        _current = current;
    }

    public async Task<IReadOnlyList<ContactInviteDto>> Handle(
        ListMyContactInvitesQuery request,
        CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var invites = await _invites.GetByUserIdAsync(_current.UserId, ct);

        return invites
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ContactInviteDto(
                x.Id,
                x.Channel,
                x.Email,
                x.PhoneNumber,
                x.DisplayName,
                x.InviteToken,
                x.Status,
                x.CreatedAtUtc,
                x.CancelledAtUtc))
            .ToList();
    }
}