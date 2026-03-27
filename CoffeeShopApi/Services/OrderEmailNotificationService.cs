using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoffeeShopApi.Models;

namespace CoffeeShopApi.Services;

public class OrderEmailNotificationService
{
    private const string ResendApiUrl = "https://api.resend.com/emails";
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrderEmailNotificationService> _logger;

    public OrderEmailNotificationService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<OrderEmailNotificationService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Resend:ApiKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Resend:From"]);

    public async Task SendOrderReceivedAsync(Order order, CancellationToken cancellationToken = default)
    {
        var subject = $"Roast66 order #{order.Id} received";
        var body = BuildOrderBody(order, "We received your order and will update you as status changes.");
        await SendEmailAsync(order.CustomerEmail, subject, body, cancellationToken);
    }

    public async Task SendReadyForPickupAsync(Order order, CancellationToken cancellationToken = default)
    {
        var subject = $"Roast66 order #{order.Id} is ready for pickup";
        var body = BuildOrderBody(order, "Your order is ready for pickup.");
        await SendEmailAsync(order.CustomerEmail, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(string? toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Resend:ApiKey"]?.Trim();
        var from = _configuration["Resend:From"]?.Trim();
        var to = toEmail?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("Order email notifications are not configured.");
        }

        var payload = new
        {
            from,
            to = new[] { to },
            subject,
            text = body
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ResendApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Order email send failed. Status: {StatusCode}. Body: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException("Failed to send order email.");
        }
    }

    private static string BuildOrderBody(Order order, string introLine)
    {
        var sb = new StringBuilder();
        sb.AppendLine(introLine);
        sb.AppendLine();
        sb.AppendLine($"Order number: {order.Id}");
        sb.AppendLine($"Order status: {order.OrderStatus}");
        sb.AppendLine();
        sb.AppendLine("Order summary:");

        decimal total = 0m;
        foreach (var item in order.OrderItems ?? [])
        {
            var itemPrice = item.MenuItem?.Price ?? 0m;
            var itemName = item.MenuItem?.Name ?? $"Item {item.MenuItemId}";
            var lineTotal = itemPrice * item.Quantity;
            total += lineTotal;
            sb.AppendLine($"- {item.Quantity}x {itemName} ({lineTotal:C})");

            foreach (var addOn in item.AddOns ?? [])
            {
                var addOnPrice = addOn.MenuItem?.Price ?? 0m;
                var addOnName = addOn.MenuItem?.Name ?? $"Add-on {addOn.MenuItemId}";
                var addOnTotal = addOnPrice * addOn.Quantity;
                total += addOnTotal;
                sb.AppendLine($"  + {addOn.Quantity}x {addOnName} ({addOnTotal:C})");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total: {total:C}");
        sb.AppendLine("Track order: /order-status (use order number + your phone)");
        return sb.ToString();
    }
}
