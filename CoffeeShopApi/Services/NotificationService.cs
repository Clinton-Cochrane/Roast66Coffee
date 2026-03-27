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
    private readonly OrderEmailNotificationService _orderEmailNotificationService;
    private readonly StaffPushNotificationService _staffPushNotificationService;

    public NotificationService(
        IConfiguration configuration,
        ApplicationDbContext context,
        NotificationSettingsService settingsService,
        TwilioService twilioService,
        OrderEmailNotificationService orderEmailNotificationService,
        StaffPushNotificationService staffPushNotificationService)
    {
        _configuration = configuration;
        _context = context;
        _settingsService = settingsService;
        _twilioService = twilioService;
        _orderEmailNotificationService = orderEmailNotificationService;
        _staffPushNotificationService = staffPushNotificationService;
    }

    public async Task SendOrderNotificationAsync(Order order, CancellationToken cancellationToken = default)
    {
        var itemCount = order.OrderItems?.Sum(oi => oi.Quantity) ?? 0;
        var staffBody = $"Roast 66: New order #{order.Id} from {order.CustomerName} ({order.CustomerPhone}), {itemCount} item(s).";
        var customerBody = $"Roast 66: We received your order #{order.Id}. Track status at /order-status.";
        var settings = await _settingsService.GetNotificationSettingsAsync(cancellationToken);
        var twilioFromPhone = NormalizePhone(settings?.TwilioFromPhoneNumber ?? string.Empty);

        var recipients = await GetStaffRecipientsAsync(cancellationToken);
        foreach (var recipient in recipients)
        {
            await SendWithLoggingAsync(
                channel: "sms",
                eventType: EventOrderCreated,
                recipientRole: recipient.Role,
                recipientPhone: recipient.Phone,
                recipientEmail: null,
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
                channel: "sms",
                eventType: EventOrderCreated,
                recipientRole: "customer",
                recipientPhone: customerPhone,
                recipientEmail: null,
                templateKey: "customer_order_received",
                body: customerBody,
                orderId: order.Id,
                payload: new { orderId = order.Id, order.CustomerName, order.CustomerPhone, itemCount },
                fromPhoneNumber: twilioFromPhone,
                cancellationToken: cancellationToken);
        }

        if (order.CustomerNotificationOptIn && !string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            await SendWithLoggingAsync(
                channel: "email",
                eventType: EventOrderCreated,
                recipientRole: "customer",
                recipientPhone: string.Empty,
                recipientEmail: order.CustomerEmail,
                templateKey: "customer_order_received_email",
                body: string.Empty,
                orderId: order.Id,
                payload: new { orderId = order.Id, order.CustomerName, order.CustomerEmail, itemCount },
                fromPhoneNumber: string.Empty,
                cancellationToken: cancellationToken);
        }

        await _staffPushNotificationService.SendNewOrderAlertAsync(order, cancellationToken);
    }

    public async Task SendReadyForPickupNotificationAsync(Order order, CancellationToken cancellationToken = default)
    {
        var customerPhone = NormalizePhone(order.CustomerPhone ?? string.Empty);
        var settings = await _settingsService.GetNotificationSettingsAsync(cancellationToken);
        var twilioFromPhone = NormalizePhone(settings?.TwilioFromPhoneNumber ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var body = $"Roast 66: Your order #{order.Id} is ready for pickup.";
            await SendWithLoggingAsync(
                channel: "sms",
                eventType: EventOrderReady,
                recipientRole: "customer",
                recipientPhone: customerPhone,
                recipientEmail: null,
                templateKey: "customer_ready_for_pickup",
                body: body,
                orderId: order.Id,
                payload: new { orderId = order.Id, order.CustomerName, order.CustomerPhone, status = order.OrderStatus.ToString() },
                fromPhoneNumber: twilioFromPhone,
                cancellationToken: cancellationToken);
        }

        if (order.CustomerNotificationOptIn && !string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            await SendWithLoggingAsync(
                channel: "email",
                eventType: EventOrderReady,
                recipientRole: "customer",
                recipientPhone: string.Empty,
                recipientEmail: order.CustomerEmail,
                templateKey: "customer_ready_for_pickup_email",
                body: string.Empty,
                orderId: order.Id,
                payload: new { orderId = order.Id, order.CustomerName, order.CustomerEmail, status = order.OrderStatus.ToString() },
                fromPhoneNumber: string.Empty,
                cancellationToken: cancellationToken);
        }
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
        string channel,
        string eventType,
        string recipientRole,
        string recipientPhone,
        string? recipientEmail,
        string templateKey,
        string body,
        int orderId,
        object payload,
        string fromPhoneNumber,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = NormalizePhone(recipientPhone);
        var normalizedEmail = NormalizeEmail(recipientEmail);
        var destination = channel == "email" ? normalizedEmail : normalizedPhone;
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        var dedupKey = BuildDedupKey(eventType, recipientRole, destination, templateKey, orderId);
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
            RecipientEmail = normalizedEmail,
            Channel = channel,
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

        if (channel == "email")
        {
            await SendEmailWithRetryAsync(message, orderId, cancellationToken);
            return;
        }

        var smsEnabled = _configuration.GetValue("Notifications:SmsEnabled", false);
        if (!smsEnabled || !_twilioService.IsConfigured())
        {
            message.Status = "skipped";
            message.LastError = !smsEnabled ? "SMS channel is disabled." : "Twilio is not configured.";
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

    private async Task SendEmailWithRetryAsync(NotificationMessage message, int orderId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns!)
            .ThenInclude(a => a.MenuItem)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            message.Status = "failed";
            message.LastError = "Order not found for email notification.";
            message.UpdatedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!_orderEmailNotificationService.IsConfigured())
        {
            message.Status = "skipped";
            message.LastError = "Order email provider is not configured.";
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
                if (message.EventType == EventOrderReady)
                {
                    await _orderEmailNotificationService.SendReadyForPickupAsync(order, cancellationToken);
                }
                else
                {
                    await _orderEmailNotificationService.SendOrderReceivedAsync(order, cancellationToken);
                }

                message.ProviderMessageId = $"email-{Guid.NewGuid():N}";
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

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }
}
