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
                var deleted = await retentionService.PurgeEmailNotificationsOlderThanAsync(
                    DateTime.UtcNow.AddDays(-30),
                    stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Purged {Count} email notification records older than 30 days.", deleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email notification retention pass failed.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
