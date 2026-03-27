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

    /// <summary>True when Stripe Checkout can be created (secret key present). Webhooks use <c>Stripe:WebhookSecret</c> separately.</summary>
    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Stripe:SecretKey"]);

    public async Task<(string checkoutUrl, string sessionId)> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        var frontendBaseUrl = (_configuration["Stripe:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

        var existingDraft = await _context.PaymentCheckoutDrafts
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingDraft != null)
        {
            var existingSession = await new SessionService()
                .GetAsync(existingDraft.CheckoutSessionId, cancellationToken: cancellationToken);
            return (existingSession.Url ?? string.Empty, existingSession.Id);
        }

        List<SessionLineItemOptions> lineItems;
        string successUrl;
        string cancelUrl;
        CheckoutSessionRequest payloadToStore;

        if (request.ExistingOrderId is int prepayId && prepayId > 0)
        {
            var order = await _orderService.GetOrderByIdAsync(prepayId, cancellationToken)
                ?? throw new InvalidOperationException("Order not found.");
            if (order.PaidUtc != null)
            {
                throw new InvalidOperationException("This order is already paid.");
            }

            var requestPhone = NormalizePhone(request.CustomerPhone);
            var orderPhone = NormalizePhone(order.CustomerPhone ?? "");
            if (string.IsNullOrEmpty(requestPhone) || requestPhone != orderPhone)
            {
                throw new InvalidOperationException("Phone number does not match this order.");
            }

            lineItems = BuildLineItemsFromOrder(order);
            if (lineItems.Count == 0)
            {
                throw new InvalidOperationException("This order has no billable items.");
            }

            successUrl = $"{frontendBaseUrl}/order-status?checkout=success&orderId={prepayId}";
            cancelUrl = $"{frontendBaseUrl}/order-status?checkout=cancelled&orderId={prepayId}";

            payloadToStore = new CheckoutSessionRequest
            {
                ExistingOrderId = prepayId,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone ?? request.CustomerPhone,
                CustomerEmail = order.CustomerEmail ?? request.CustomerEmail,
                CustomerNotificationOptIn = order.CustomerNotificationOptIn || request.CustomerNotificationOptIn,
                OrderItems = []
            };
        }
        else
        {
            if (request.OrderItems == null || request.OrderItems.Count == 0)
            {
                throw new InvalidOperationException("Order items are required for a new checkout.");
            }

            lineItems = await BuildLineItemsFromRequestAsync(request, cancellationToken);
            successUrl = $"{frontendBaseUrl}/order/confirmation?checkout=success";
            cancelUrl = $"{frontendBaseUrl}/order?checkout=cancelled";
            payloadToStore = request;
        }

        var sessionService = new SessionService();
        var metadata = new Dictionary<string, string>
        {
            ["customer_name"] = payloadToStore.CustomerName,
            ["customer_phone"] = payloadToStore.CustomerPhone
        };
        if (!string.IsNullOrWhiteSpace(payloadToStore.CustomerEmail))
        {
            metadata["customer_email"] = payloadToStore.CustomerEmail;
        }
        metadata["customer_notification_opt_in"] = payloadToStore.CustomerNotificationOptIn ? "true" : "false";
        if (payloadToStore.ExistingOrderId is int oid && oid > 0)
        {
            metadata["existing_order_id"] = oid.ToString();
        }

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = lineItems,
            Metadata = metadata
        };

        var session = await sessionService.CreateAsync(
            options,
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        var draft = new PaymentCheckoutDraft
        {
            CheckoutSessionId = session.Id,
            IdempotencyKey = idempotencyKey,
            CustomerName = payloadToStore.CustomerName,
            CustomerPhone = payloadToStore.CustomerPhone,
            PayloadJson = JsonSerializer.Serialize(payloadToStore),
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

        if (payload.ExistingOrderId is int prepayOrderId && prepayOrderId > 0)
        {
            var order = await _orderService.GetOrderByIdAsync(prepayOrderId, cancellationToken);
            if (order == null)
            {
                throw new InvalidOperationException("Prepay order no longer exists.");
            }

            if (order.PaidUtc != null)
            {
                draft.Status = "completed";
                draft.CompletedUtc = DateTime.UtcNow;
                draft.OrderId = order.Id;
                draft.StripePaymentIntentId = paymentIntentId;
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            order.PaidUtc = DateTime.UtcNow;
            order.StripePaymentIntentId = paymentIntentId;
            _context.Orders.Update(order);

            draft.Status = "completed";
            draft.CompletedUtc = DateTime.UtcNow;
            draft.StripePaymentIntentId = paymentIntentId;
            draft.OrderId = order.Id;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var orderNew = new Order
        {
            CustomerName = payload.CustomerName,
            CustomerPhone = payload.CustomerPhone,
            CustomerEmail = payload.CustomerEmail,
            CustomerNotificationOptIn = payload.CustomerNotificationOptIn,
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

        var createdOrder = await _orderService.CreateOrderAsync(orderNew);
        await _notificationService.SendOrderNotificationAsync(createdOrder, cancellationToken);
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

    private async Task<List<SessionLineItemOptions>> BuildLineItemsFromRequestAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
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

        return lineItems;
    }

    private static List<SessionLineItemOptions> BuildLineItemsFromOrder(Order order)
    {
        var lineItems = new List<SessionLineItemOptions>();
        foreach (var item in order.OrderItems ?? [])
        {
            var menuItem = item.MenuItem
                ?? throw new InvalidOperationException($"Order item {item.Id} is missing menu data.");
            lineItems.Add(ToLineItem(menuItem.Name, menuItem.Description, menuItem.Price, item.Quantity));

            foreach (var addOn in item.AddOns ?? [])
            {
                var addOnItem = addOn.MenuItem
                    ?? throw new InvalidOperationException($"Add-on {addOn.Id} is missing menu data.");
                lineItems.Add(ToLineItem(
                    $"{menuItem.Name} add-on: {addOnItem.Name}",
                    addOnItem.Description,
                    addOnItem.Price,
                    addOn.Quantity));
            }
        }

        return lineItems;
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

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());
}
