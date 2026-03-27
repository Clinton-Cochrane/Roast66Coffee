using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace CoffeeShopApi.Services;

public class StaffPushNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StaffPushNotificationService> _logger;

    public StaffPushNotificationService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<StaffPushNotificationService> logger)
    {
        _context = context;
        _configuration = configuration;
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

    public async Task SendNewOrderAlertAsync(Order order, CancellationToken cancellationToken = default)
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
            body = $"Order #{order.Id} from {order.CustomerName}",
            tag = "new-order",
            orderId = order.Id,
            url = "/admin"
        });

        var vapidDetails = new VapidDetails(
            _configuration["Push:Subject"]!,
            _configuration["Push:VapidPublicKey"]!,
            _configuration["Push:VapidPrivateKey"]!);
        var client = new WebPushClient();
        var deadEndpoints = new List<string>();

        foreach (var sub in subscriptions)
        {
            try
            {
                var webPushSubscription = new PushSubscription(sub.Endpoint, sub.P256Dh, sub.Auth);
                await client.SendNotificationAsync(webPushSubscription, payload, vapidDetails, cancellationToken: cancellationToken);
            }
            catch (WebPushException ex)
            {
                // 404/410 means the browser subscription is gone and safe to cleanup.
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    deadEndpoints.Add(sub.Endpoint);
                }
                else
                {
                    _logger.LogWarning(ex, "Push send failed for endpoint {Endpoint}", sub.Endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected push send failure for endpoint {Endpoint}", sub.Endpoint);
            }
        }

        if (deadEndpoints.Count > 0)
        {
            var stale = await _context.StaffPushSubscriptions
                .Where(x => deadEndpoints.Contains(x.Endpoint))
                .ToListAsync(cancellationToken);
            if (stale.Count > 0)
            {
                _context.StaffPushSubscriptions.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
