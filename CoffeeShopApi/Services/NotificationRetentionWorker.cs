namespace CoffeeShopApi.Services;

public class NotificationRetentionWorker : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationRetentionWorker> _logger;

    public NotificationRetentionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<NotificationRetentionService>();
                var deleted = await retentionService.PurgeNotificationsOlderThanAsync(
                    DateTime.UtcNow.AddDays(-NotificationRetentionService.RetentionDays),
                    stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Purged {Count} notification records older than {RetentionDays} days.",
                        deleted,
                        NotificationRetentionService.RetentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Notification retention pass failed. Failure type: {FailureType}.",
                    ex.GetType().Name);
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
