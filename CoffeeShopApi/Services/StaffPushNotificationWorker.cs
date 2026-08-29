namespace CoffeeShopApi.Services;

internal sealed class StaffPushNotificationWorker : BackgroundService
{
    private readonly StaffPushNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaffPushNotificationWorker> _logger;

    public StaffPushNotificationWorker(
        StaffPushNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<StaffPushNotificationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var pushService = scope.ServiceProvider.GetRequiredService<StaffPushNotificationService>();
                await pushService.SendNewOrderAlertAsync(notification.OrderId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Staff push dispatch failed for order {OrderId}. Failure type: {FailureType}.",
                    notification.OrderId,
                    ex.GetType().Name);
            }
        }
    }
}
