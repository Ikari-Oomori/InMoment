using InMoment.Application.Abstractions.Communication;
using InMoment.Domain.Common;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class DisabledContactInviteSender : IContactInviteSender
{
    private readonly ILogger<DisabledContactInviteSender> _logger;

    public DisabledContactInviteSender(ILogger<DisabledContactInviteSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(ContactInviteSendRequest request, CancellationToken ct)
    {
        _logger.LogWarning(
            "Contact invite delivery is disabled. Channel: {Channel}; Email: {Email}; Phone: {Phone}",
            request.Channel,
            request.Email,
            request.PhoneNumber);

        throw new ValidationException(
            "Отправка приглашений на email или телефон пока недоступна. Пригласите пользователя по username или коду группы.");
    }
}