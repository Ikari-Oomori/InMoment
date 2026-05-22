using InMoment.Application.Abstractions.Communication;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class DevContactInviteSender : IContactInviteSender
{
    private readonly ILogger<DevContactInviteSender> _logger;

    public DevContactInviteSender(ILogger<DevContactInviteSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(ContactInviteSendRequest request, CancellationToken ct)
    {
        _logger.LogWarning(
            "DEV CONTACT INVITE. Channel: {Channel}; Email: {Email}; Phone: {Phone}; DisplayName: {DisplayName}; Link: {InviteLink}",
            request.Channel,
            request.Email,
            request.PhoneNumber,
            request.DisplayName,
            request.InviteLink);

        return Task.CompletedTask;
    }
}