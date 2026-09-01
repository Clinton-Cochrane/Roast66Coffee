using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace CoffeeShopApi.Services;

/// <summary>
/// Fans a staff alert out independently to every registered browser subscription.
/// One device failure never blocks another; expired endpoints are removed, while
/// transient failures are retried within configured timeout and attempt bounds.
/// </summary>
public class StaffPushNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IStaffPushSender _sender;
    private readonly StaffPushOptions _options;
    private readonly ILogger<StaffPushNotificationService> _logger;

    public StaffPushNotificationService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IStaffPushSender sender,
        IOptions<StaffPushOptions> options,
        ILogger<StaffPushNotificationService> logger)
    {
        _context = context;
        _configuration = configuration;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Push:VapidPublicKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Push:VapidPrivateKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Push:Subject"]);

    public string? GetPublicKey() => _configuration["Push:VapidPublicKey"];

    public async Task UpsertSubscriptionAsync(
        string endpoint,
        string p256dh,
        string auth,
        string? userIdentifier,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.StaffPushSubscriptions
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint, cancellationToken);

        if (existing == null)
        {
            _context.StaffPushSubscriptions.Add(new StaffPushSubscription
            {
                Endpoint = endpoint,
                P256Dh = p256dh,
                Auth = auth,
                UserIdentifier = userIdentifier,
                UserAgent = userAgent,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.P256Dh = p256dh;
            existing.Auth = auth;
            existing.UserIdentifier = userIdentifier;
            existing.UserAgent = userAgent;
            existing.UpdatedUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveSubscriptionAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var existing = await _context.StaffPushSubscriptions
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint, cancellationToken);
        if (existing == null)
        {
            return;
        }

        _context.StaffPushSubscriptions.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SendNewOrderAlertAsync(int orderId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return;
        }

        var subscriptions = await _context.StaffPushSubscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "New Roast66 order",
            body = $"Order #{orderId} is ready to review",
            tag = $"new-order-{orderId}",
            orderId,
            url = "/admin"
        });

        var deadSubscriptionIds = new List<Guid>();
        var failedSubscriptionCount = 0;

        foreach (var sub in subscriptions)
        {
            var delivered = false;
            for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
            {
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(_options.RequestTimeout);

                try
                {
                    await _sender.SendAsync(sub, payload, requestTimeout.Token);
                    delivered = true;
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "Staff push recovered for order {OrderId}, subscription {SubscriptionId}, on attempt {Attempt}.",
                            orderId,
                            sub.Id,
                            attempt);
                    }
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (WebPushException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    deadSubscriptionIds.Add(sub.Id);
                    delivered = true;
                    break;
                }
                catch (OperationCanceledException)
                {
                    LogAttemptFailure(orderId, sub.Id, attempt, "timeout");
                }
                catch (Exception ex)
                {
                    LogAttemptFailure(orderId, sub.Id, attempt, ex.GetType().Name);
                }

                if (attempt < _options.MaxAttempts)
                {
                    await Task.Delay(_options.RetryDelay, cancellationToken);
                }
            }

            if (!delivered)
            {
                failedSubscriptionCount++;
            }
        }

        if (deadSubscriptionIds.Count > 0)
        {
            var stale = await _context.StaffPushSubscriptions
                .Where(x => deadSubscriptionIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
            if (stale.Count > 0)
            {
                _context.StaffPushSubscriptions.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        if (failedSubscriptionCount > 0)
        {
            _logger.LogWarning(
                "Staff push delivery failed for {FailedSubscriptionCount} of {SubscriptionCount} subscriptions for order {OrderId}.",
                failedSubscriptionCount,
                subscriptions.Count,
                orderId);
        }
    }

    private void LogAttemptFailure(int orderId, Guid subscriptionId, int attempt, string failureType)
    {
        _logger.LogWarning(
            "Staff push attempt {Attempt} of {MaxAttempts} failed for order {OrderId}, subscription {SubscriptionId}. Failure type: {FailureType}.",
            attempt,
            _options.MaxAttempts,
            orderId,
            subscriptionId,
            failureType);
    }
}
