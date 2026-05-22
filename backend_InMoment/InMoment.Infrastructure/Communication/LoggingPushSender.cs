using InMoment.Application.Abstractions.Communication;
using Microsoft.Extensions.Logging;

namespace InMoment.Infrastructure.Communication;

public sealed class LoggingPushSender : IPushSender
{
    private readonly ILogger<LoggingPushSender> _logger;

    public LoggingPushSender(ILogger<LoggingPushSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(PushSendRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Push notification logged only. UserId={UserId}, Type={Type}, Targets={TargetsCount}, Title={Title}",
            request.UserId,
            request.NotificationType,
            request.Targets.Count,
            request.Title);

        return Task.CompletedTask;
    }
}