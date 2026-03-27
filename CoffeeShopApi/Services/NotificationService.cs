using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services;

public class NotificationService
{
    private const string EventOrderCreated = "order.created";
    private const string EventOrderReady = "order.ready_for_pickup";

    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly NotificationSettingsService _settingsService;
    private readonly TwilioService _twilioService;

    public NotificationService(
        IConfiguration configuration,
        ApplicationDbContext context,
        NotificationSettingsService settingsService,
        TwilioService twilioService)
    {
        _configuration = configuration;
        _context = context;
        _settingsService = settingsService;
        _twilioService = twilioService;
    }

    public async Task SendOrderNotificationAsync(Order order, CancellationToken cancellationToken = default)
    {
        var itemCount = order.OrderItems?.Sum(oi => oi.Quantity) ?? 0;
        var staffBody = $"Roast 66: New order #{order.Id} from {order.CustomerName} ({order.CustomerPhone}), {itemCount} item(s).";
        var customerBody = $"Roast 66: We received your order #{order.Id}. We will text you when it is ready for pickup.";
        var settings = await _settingsService.GetNotificationSettingsAsync(cancellationToken);
        var twilioFromPhone = NormalizePhone(settings?.TwilioFromPhoneNumber ?? string.Empty);

        var recipients = await GetStaffRecipientsAsync(cancellationToken);
        foreach (var recipient in recipients)
        {
            await SendWithLoggingAsync(
                eventType: EventOrderCreated,
                recipientRole: recipient.Role,
                recipientPhone: recipient.Phone,
                templateKey: "staff_new_order",
                body: staffBody,
                orderId: order.Id,
                payload: new { orderId = order.Id, order.CustomerName, order.CustomerPhone, itemCount },
                fromPhoneNumber: twilioFromPhone,
                cancellationToken: cancellationToken);
        }

        var customerPhone = NormalizePhone(order.CustomerPhone ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            await SendWithLoggingAsync(
                eventType: EventOrderCreated,
                recipientRole: "customer",
                recipientPhone: customerPhone,
                templateKey: "customer_order_received",
                body: customerBody,
                orderId: order.Id,
                payload: new { orderId = order.Id, order.CustomerName, order.CustomerPhone, itemCount },
                fromPhoneNumber: twilioFromPhone,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendReadyForPickupNotificationAsync(Order order, CancellationToken cancellationToken = default)
    {
        var customerPhone = NormalizePhone(order.CustomerPhone ?? string.Empty);
        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            return;
        }
        var settings = await _settingsService.GetNotificationSettingsAsync(cancellationToken);
        var twilioFromPhone = NormalizePhone(settings?.TwilioFromPhoneNumber ?? string.Empty);

        var body = $"Roast 66: Your order #{order.Id} is ready for pickup.";
        await SendWithLoggingAsync(
            eventType: EventOrderReady,
            recipientRole: "customer",
            recipientPhone: customerPhone,
            templateKey: "customer_ready_for_pickup",
            body: body,
            orderId: order.Id,
            payload: new { orderId = order.Id, order.CustomerName, order.CustomerPhone, status = order.OrderStatus.ToString() },
            fromPhoneNumber: twilioFromPhone,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationMessage>> GetNotificationsForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationMessages
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationMessage>> GetCustomerNotificationsForOrderAsync(
        int orderId,
        string phone,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);
        return await _context.NotificationMessages
            .Where(x => x.OrderId == orderId && x.RecipientRole == "customer" && x.RecipientPhone == normalizedPhone)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateProviderStatusAsync(
        string providerMessageId,
        string providerStatus,
        string? providerError,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
        {
            return;
        }

        var existing = await _context.NotificationMessages
            .Where(x => x.ProviderMessageId == providerMessageId)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing == null)
        {
            return;
        }

        existing.Status = providerStatus.ToLowerInvariant();
        existing.LastError = string.IsNullOrWhiteSpace(providerError) ? existing.LastError : providerError;
        existing.UpdatedUtc = DateTime.UtcNow;
        if (providerStatus.Equals("delivered", StringComparison.OrdinalIgnoreCase))
        {
            existing.SentUtc ??= DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SendWithLoggingAsync(
        string eventType,
        string recipientRole,
        string recipientPhone,
        string templateKey,
        string body,
        int orderId,
        object payload,
        string fromPhoneNumber,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = NormalizePhone(recipientPhone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return;
        }

        var dedupKey = BuildDedupKey(eventType, recipientRole, normalizedPhone, templateKey, orderId);
        var existing = await _context.NotificationMessages
            .FirstOrDefaultAsync(x => x.DedupKey == dedupKey, cancellationToken);
        if (existing != null)
        {
            return;
        }

        var message = new NotificationMessage
        {
            EventType = eventType,
            RecipientRole = recipientRole,
            RecipientPhone = normalizedPhone,
            TemplateKey = templateKey,
            OrderId = orderId,
            PayloadJson = JsonSerializer.Serialize(payload),
            DedupKey = dedupKey,
            Status = "pending",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _context.NotificationMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        if (!_twilioService.IsConfigured())
        {
            message.Status = "skipped";
            message.LastError = "Twilio is not configured.";
            message.UpdatedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            message.AttemptCount = attempt;
            try
            {
                message.ProviderMessageId = await _twilioService.SendSmsAsync(normalizedPhone, body, fromPhoneNumber);
                message.Status = "sent";
                message.LastError = null;
                message.SentUtc = DateTime.UtcNow;
                message.UpdatedUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                message.Status = attempt == maxAttempts ? "failed" : "retrying";
                message.LastError = ex.Message;
                message.UpdatedUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                }
            }
        }
    }

    private async Task<IReadOnlyList<(string Role, string Phone)>> GetStaffRecipientsAsync(CancellationToken cancellationToken)
    {
        var recipients = new List<(string Role, string Phone)>();
        var settings = await _settingsService.GetNotificationSettingsAsync(cancellationToken);

        AddIfPresent(recipients, "admin", settings?.AdminPhoneNumber ?? _configuration["Twilio:AdminPhoneNumber"]);
        AddIfPresent(recipients, "barista", settings?.BaristaPhoneNumber);
        AddIfPresent(recipients, "trailer", settings?.TrailerPhoneNumber);
        return recipients;
    }

    private static void AddIfPresent(List<(string Role, string Phone)> recipients, string role, string? rawPhone)
    {
        var phone = NormalizePhone(rawPhone ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            recipients.Add((role, phone));
        }
    }

    private static string BuildDedupKey(string eventType, string role, string phone, string templateKey, int orderId)
    {
        var raw = $"{eventType}|{role}|{phone}|{templateKey}|{orderId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        if (digits.Length == 10)
        {
            return $"+1{digits}";
        }

        if (digits.Length == 11 && digits.StartsWith('1'))
        {
            return $"+{digits}";
        }

        return digits.StartsWith('+') ? digits : $"+{digits}";
    }
}
