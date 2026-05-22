using InMoment.Domain.Contacts;

namespace InMoment.Application.Abstractions.Communication;

public interface IContactInviteSender
{
    Task SendAsync(ContactInviteSendRequest request, CancellationToken ct);
}

public sealed record ContactInviteSendRequest(
    ContactInviteChannel Channel,
    string? Email,
    string? PhoneNumber,
    string? DisplayName,
    string InviteLink);