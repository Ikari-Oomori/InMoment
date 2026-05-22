using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Notifications;

public sealed class SystemNotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SystemNotificationOptions> _options;
    private readonly ILogger<SystemNotificationHostedService> _logger;

    public SystemNotificationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SystemNotificationOptions> options,
        ILogger<SystemNotificationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogInformation("System notifications hosted service is disabled.");
            return;
        }

        if (options.RunOnStartupDelaySeconds > 0)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(options.RunOnStartupDelaySeconds),
                stoppingToken);
        }

        await RunOnceAsync(stoppingToken);

        var intervalMinutes = options.IntervalMinutes <= 0
            ? 360
            : options.IntervalMinutes;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<SystemNotificationProcessor>();
            await processor.ProcessAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System notification background iteration failed.");
        }
    }
}