using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace CoffeeShopApi.Services;

public interface IStaffPushNotificationQueue
{
    bool TryEnqueue(int orderId);
}

internal sealed record StaffPushNotification(int OrderId);

/// <summary>
/// Bounded, best-effort, in-process handoff from order creation to the push worker.
/// Deduplication deliberately lasts only for the configured window and is not durable
/// across restarts; the customer order succeeds even when this queue is full.
/// </summary>
internal sealed class StaffPushNotificationQueue : IStaffPushNotificationQueue
{
    private readonly Channel<StaffPushNotification> _channel;
    private readonly Queue<(int OrderId, DateTime ExpiresUtc)> _recentOrders = new();
    private readonly HashSet<int> _recentOrderIds = [];
    private readonly object _gate = new();
    private readonly StaffPushOptions _options;
    private readonly ILogger<StaffPushNotificationQueue> _logger;

    public StaffPushNotificationQueue(
        IOptions<StaffPushOptions> options,
        ILogger<StaffPushNotificationQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<StaffPushNotification>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(int orderId)
    {
        lock (_gate)
        {
            RemoveExpiredDeduplicationEntries(DateTime.UtcNow);
            if (_recentOrderIds.Contains(orderId))
            {
                _logger.LogDebug("Skipping duplicate staff push dispatch for order {OrderId}.", orderId);
                return false;
            }

            if (!_channel.Writer.TryWrite(new StaffPushNotification(orderId)))
            {
                _logger.LogWarning(
                    "Staff push queue is full; dropping best-effort notification for order {OrderId}.",
                    orderId);
                return false;
            }

            RememberOrder(orderId, DateTime.UtcNow.Add(_options.DeduplicationWindow));
            return true;
        }
    }

    public IAsyncEnumerable<StaffPushNotification> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    private void RememberOrder(int orderId, DateTime expiresUtc)
    {
        while (_recentOrders.Count >= _options.DeduplicationCapacity)
        {
            RemoveOldestDeduplicationEntry();
        }

        _recentOrderIds.Add(orderId);
        _recentOrders.Enqueue((orderId, expiresUtc));
    }

    private void RemoveExpiredDeduplicationEntries(DateTime utcNow)
    {
        while (_recentOrders.TryPeek(out var entry) && entry.ExpiresUtc <= utcNow)
        {
            RemoveOldestDeduplicationEntry();
        }
    }

    private void RemoveOldestDeduplicationEntry()
    {
        var entry = _recentOrders.Dequeue();
        _recentOrderIds.Remove(entry.OrderId);
    }
}
