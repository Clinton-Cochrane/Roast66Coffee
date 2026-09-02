using Microsoft.Extensions.Options;

namespace CoffeeShopApi.Services;

/// <summary>
/// Periodically enforces data retention. Failures are classified without logging
/// exception messages and retried sooner than the normal hourly schedule.
/// </summary>
public sealed class DataRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<DataRetentionWorker> _logger;

    public DataRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.Zero;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<DataRetentionService>();
                var result = await retentionService.PurgeExpiredDataAsync(stoppingToken);

                _logger.LogInformation(
                    "Data retention completed in {BatchCount} batches. Deleted {OrderCount} completed orders, {PaymentCount} payments, {NotificationCount} notification logs, and {AuditCount} audit events.",
                    result.BatchesCompleted,
                    result.CompletedOrdersDeleted,
                    result.PaymentsDeleted,
                    result.NotificationLogsDeleted,
                    result.AuditEventsDeleted);
                delay = TimeSpan.FromMinutes(_options.CheckIntervalMinutes);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Data retention failed. Failure type: {FailureType}. Retrying after {RetryDelayMinutes} minutes.",
                    ex.GetType().Name,
                    _options.RetryDelayMinutes);
                delay = TimeSpan.FromMinutes(_options.RetryDelayMinutes);
            }
        }
    }
}
