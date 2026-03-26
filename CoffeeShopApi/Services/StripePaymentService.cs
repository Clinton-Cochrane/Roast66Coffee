using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace CoffeeShopApi.Services;

public class StripePaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly OrderService _orderService;
    private readonly NotificationService _notificationService;

    public StripePaymentService(
        ApplicationDbContext context,
        IConfiguration configuration,
        OrderService orderService,
        NotificationService notificationService)
    {
        _context = context;
        _configuration = configuration;
        _orderService = orderService;
        _notificationService = notificationService;
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Stripe:SecretKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Stripe:WebhookSecret"]);

    public async Task<(string checkoutUrl, string sessionId)> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        var frontendBaseUrl = _configuration["Stripe:FrontendBaseUrl"] ?? "http://localhost:3000";
        var existingDraft = await _context.PaymentCheckoutDrafts
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingDraft != null)
        {
            var existingSession = await new SessionService()
                .GetAsync(existingDraft.CheckoutSessionId, cancellationToken: cancellationToken);
            return (existingSession.Url ?? string.Empty, existingSession.Id);
        }

        var menuIds = request.OrderItems
            .Select(x => x.MenuItemId)
            .Concat(request.OrderItems.SelectMany(x => x.AddOns.Select(a => a.MenuItemId)))
            .Distinct()
            .ToList();

        var menuLookup = await _context.MenuItems
            .Where(m => menuIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var lineItems = new List<SessionLineItemOptions>();
        foreach (var item in request.OrderItems)
        {
            if (!menuLookup.TryGetValue(item.MenuItemId, out var menuItem))
            {
                throw new InvalidOperationException($"Menu item {item.MenuItemId} not found.");
            }

            lineItems.Add(ToLineItem(menuItem.Name, menuItem.Description, menuItem.Price, item.Quantity));

            foreach (var addOn in item.AddOns)
            {
                if (!menuLookup.TryGetValue(addOn.MenuItemId, out var addOnItem))
                {
                    throw new InvalidOperationException($"Add-on item {addOn.MenuItemId} not found.");
                }

                lineItems.Add(ToLineItem(
                    $"{menuItem.Name} add-on: {addOnItem.Name}",
                    addOnItem.Description,
                    addOnItem.Price,
                    addOn.Quantity));
            }
        }

        var sessionService = new SessionService();
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{frontendBaseUrl}/order/confirmation?checkout=success",
            CancelUrl = $"{frontendBaseUrl}/order?checkout=cancelled",
            LineItems = lineItems,
            Metadata = new Dictionary<string, string>
            {
                ["customer_name"] = request.CustomerName,
                ["customer_phone"] = request.CustomerPhone
            }
        };

        var session = await sessionService.CreateAsync(
            options,
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        var draft = new PaymentCheckoutDraft
        {
            CheckoutSessionId = session.Id,
            IdempotencyKey = idempotencyKey,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            PayloadJson = JsonSerializer.Serialize(request),
            Status = "pending"
        };
        _context.PaymentCheckoutDrafts.Add(draft);
        await _context.SaveChangesAsync(cancellationToken);

        return (session.Url ?? string.Empty, session.Id);
    }

    public async Task HandleCheckoutCompletedAsync(
        string checkoutSessionId,
        string? paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var draft = await _context.PaymentCheckoutDrafts
            .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId, cancellationToken);
        if (draft == null || draft.Status == "completed")
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<CheckoutSessionRequest>(draft.PayloadJson);
        if (payload == null)
        {
            throw new InvalidOperationException("Unable to deserialize checkout draft payload.");
        }

        var order = new Order
        {
            CustomerName = payload.CustomerName,
            CustomerPhone = payload.CustomerPhone,
            OrderItems = payload.OrderItems.Select(item => new OrderItem
            {
                MenuItemId = item.MenuItemId,
                Quantity = item.Quantity,
                Notes = item.Notes,
                AddOns = item.AddOns.Select(addOn => new AddOn
                {
                    MenuItemId = addOn.MenuItemId,
                    Quantity = addOn.Quantity
                }).ToList()
            }).ToList()
        };

        var createdOrder = await _orderService.CreateOrderAsync(order);
        await _notificationService.SendOrderNotificationAsync(createdOrder);
        draft.Status = "completed";
        draft.CompletedUtc = DateTime.UtcNow;
        draft.StripePaymentIntentId = paymentIntentId;
        draft.OrderId = createdOrder.Id;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task HandlePaymentFailedAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var draft = await _context.PaymentCheckoutDrafts
            .FirstOrDefaultAsync(x => x.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (draft == null || draft.Status == "completed")
        {
            return;
        }

        draft.Status = "failed";
        draft.FailedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static SessionLineItemOptions ToLineItem(string name, string? description, decimal price, int quantity)
    {
        return new SessionLineItemOptions
        {
            Quantity = quantity,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmount = (long)Math.Round(price * 100M),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = name,
                    Description = description
                }
            }
        };
    }
}
