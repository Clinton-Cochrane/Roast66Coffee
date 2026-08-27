using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services.Payments;

public sealed class PaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly OrderService _orderService;
    private readonly NotificationService _notificationService;
    private readonly IReadOnlyDictionary<string, IPaymentGateway> _gateways;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ApplicationDbContext context,
        IConfiguration configuration,
        OrderService orderService,
        NotificationService notificationService,
        IEnumerable<IPaymentGateway> gateways,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _configuration = configuration;
        _orderService = orderService;
        _notificationService = notificationService;
        _gateways = gateways.ToDictionary(
            gateway => gateway.ProviderName,
            StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public string DefaultProviderName =>
        (_configuration["Payments:DefaultProvider"] ?? "stripe").Trim();

    public bool IsConfigured(string? providerName = null) =>
        TryGetGateway(providerName, out var gateway) && gateway.IsConfigured();

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(
        CheckoutSessionRequest request,
        string idempotencyKey,
        string? providerName = null,
        CancellationToken cancellationToken = default)
    {
        var gateway = GetGateway(providerName);
        if (!gateway.IsConfigured())
        {
            throw new PaymentProviderUnavailableException(
                $"{gateway.ProviderName} is not configured for this environment.");
        }

        var existingPayment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.Provider == gateway.ProviderName &&
                           payment.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existingPayment != null)
        {
            var existingCheckout = await gateway.GetCheckoutAsync(
                existingPayment.ProviderCheckoutId,
                cancellationToken);
            return new PaymentCheckoutResult(
                existingCheckout.CheckoutUrl,
                existingCheckout.ProviderCheckoutId,
                gateway.ProviderName);
        }

        var prepared = await PrepareCheckoutAsync(request, cancellationToken);
        var payment = new Payment
        {
            Provider = gateway.ProviderName,
            Method = "online",
            Status = PaymentStatuses.Pending,
            Amount = prepared.LineItems.Sum(item => item.UnitPrice * item.Quantity),
            Currency = "USD",
            IdempotencyKey = idempotencyKey,
            CustomerName = prepared.Payload.CustomerName,
            CustomerPhone = prepared.Payload.CustomerPhone ?? string.Empty,
            PayloadJson = JsonSerializer.Serialize(prepared.Payload)
        };

        var metadata = new Dictionary<string, string>
        {
            ["customer_name"] = prepared.Payload.CustomerName,
            ["customer_notification_opt_in"] = prepared.Payload.CustomerNotificationOptIn ? "true" : "false"
        };
        if (!string.IsNullOrWhiteSpace(prepared.Payload.CustomerPhone))
        {
            metadata["customer_phone"] = prepared.Payload.CustomerPhone;
        }
        if (!string.IsNullOrWhiteSpace(prepared.Payload.CustomerEmail))
        {
            metadata["customer_email"] = prepared.Payload.CustomerEmail;
        }
        if (prepared.Payload.ExistingOrderId is int orderId && orderId > 0)
        {
            metadata["existing_order_id"] = orderId.ToString();
        }

        var checkout = await gateway.CreateCheckoutAsync(
            new GatewayCheckoutRequest(
                payment.Id,
                prepared.SuccessUrl,
                prepared.CancelUrl,
                prepared.LineItems,
                metadata),
            idempotencyKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(checkout.CheckoutUrl) ||
            string.IsNullOrWhiteSpace(checkout.ProviderCheckoutId))
        {
            throw new InvalidOperationException(
                $"{gateway.ProviderName} returned an invalid checkout response.");
        }

        payment.ProviderCheckoutId = checkout.ProviderCheckoutId;
        payment.ProviderPaymentId = checkout.ProviderPaymentId;
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentCheckoutResult(
            checkout.CheckoutUrl,
            checkout.ProviderCheckoutId,
            gateway.ProviderName);
    }

    public async Task HandleWebhookAsync(
        string? providerName,
        string body,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var gateway = GetGateway(providerName);
        var paymentEvent = await gateway.ParseWebhookAsync(body, headers, cancellationToken);
        if (paymentEvent == null)
        {
            return;
        }

        var payment = await FindPaymentAsync(gateway.ProviderName, paymentEvent, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning(
                "Ignoring {Provider} payment event because no local payment matched checkout {CheckoutId} or payment {PaymentId}.",
                gateway.ProviderName,
                paymentEvent.ProviderCheckoutId,
                paymentEvent.ProviderPaymentId);
            if (paymentEvent.PaymentId.HasValue)
            {
                throw new PaymentWebhookRetryException(
                    "The local payment record is not available yet.");
            }
            return;
        }

        payment.ProviderPaymentId ??= paymentEvent.ProviderPaymentId;
        if (!string.IsNullOrWhiteSpace(paymentEvent.Method))
        {
            payment.Method = paymentEvent.Method;
        }

        switch (paymentEvent.Status)
        {
            case GatewayPaymentStatus.Paid:
                await CompletePaymentAsync(payment, cancellationToken);
                break;
            case GatewayPaymentStatus.Failed when payment.Status != PaymentStatuses.Paid:
                payment.Status = PaymentStatuses.Failed;
                payment.FailedUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                break;
            case GatewayPaymentStatus.Pending:
                break;
        }
    }

    private async Task<PreparedCheckout> PrepareCheckoutAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var frontendBaseUrl = (
            _configuration["Payments:FrontendBaseUrl"] ??
            _configuration["Stripe:FrontendBaseUrl"] ??
            "http://localhost:3000").TrimEnd('/');

        if (request.ExistingOrderId is int existingOrderId && existingOrderId > 0)
        {
            var order = await _orderService.GetOrderByIdAsync(existingOrderId, cancellationToken)
                ?? throw new InvalidOperationException("Order not found.");
            if (order.PaidUtc != null)
            {
                throw new InvalidOperationException("This order is already paid.");
            }

            ValidateCustomerIdentity(request, order);
            var lineItems = BuildLineItemsFromOrder(order);
            if (lineItems.Count == 0)
            {
                throw new InvalidOperationException("This order has no billable items.");
            }

            return new PreparedCheckout(
                new CheckoutSessionRequest
                {
                    ExistingOrderId = existingOrderId,
                    CustomerName = order.CustomerName,
                    CustomerPhone = order.CustomerPhone ?? request.CustomerPhone ?? string.Empty,
                    CustomerEmail = order.CustomerEmail ?? request.CustomerEmail,
                    CustomerNotificationOptIn = order.CustomerNotificationOptIn || request.CustomerNotificationOptIn,
                    OrderItems = []
                },
                lineItems,
                $"{frontendBaseUrl}/order-status?checkout=success&token={order.TrackingToken}",
                $"{frontendBaseUrl}/order-status?checkout=cancelled&token={order.TrackingToken}");
        }

        if (request.OrderItems.Count == 0)
        {
            throw new InvalidOperationException("Order items are required for a new checkout.");
        }

        var requestedLineItems = await BuildLineItemsFromRequestAsync(request, cancellationToken);
        return new PreparedCheckout(
            request,
            requestedLineItems,
            $"{frontendBaseUrl}/order/confirmation?checkout=success",
            $"{frontendBaseUrl}/order?checkout=cancelled");
    }

    private async Task CompletePaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatuses.Paid)
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<CheckoutSessionRequest>(payment.PayloadJson)
            ?? throw new InvalidOperationException("Unable to deserialize payment checkout payload.");
        var paidUtc = DateTime.UtcNow;
        Order? createdOrderToNotify = null;

        if (payload.ExistingOrderId is int existingOrderId && existingOrderId > 0)
        {
            var order = await _orderService.GetOrderByIdAsync(existingOrderId, cancellationToken)
                ?? throw new InvalidOperationException("Prepay order no longer exists.");

            order.PaidUtc ??= paidUtc;
            order.PaymentProvider ??= payment.Provider;
            order.PaymentReference ??= payment.ProviderPaymentId ?? payment.ProviderCheckoutId;
            payment.OrderId = order.Id;
            _context.Orders.Update(order);
        }
        else
        {
            var order = new Order
            {
                CustomerName = payload.CustomerName,
                CustomerPhone = payload.CustomerPhone,
                CustomerEmail = payload.CustomerEmail,
                CustomerNotificationOptIn = payload.CustomerNotificationOptIn,
                PaidUtc = paidUtc,
                PaymentProvider = payment.Provider,
                PaymentReference = payment.ProviderPaymentId ?? payment.ProviderCheckoutId,
                OrderItems = payload.OrderItems.Select(item => new OrderItem
                {
                    MenuItemId = item.MenuItemId,
                    Quantity = item.Quantity,
                    Notes = item.Notes,
                    UnitPrice = item.UnitPrice ?? 0,
                    AddOns = item.AddOns.Select(addOn => new AddOn
                    {
                        MenuItemId = addOn.MenuItemId,
                        Quantity = addOn.Quantity,
                        UnitPrice = addOn.UnitPrice ?? 0
                    }).ToList()
                }).ToList()
            };

            var createdOrder = await _orderService.CreateOrderAsync(order, preserveSnapshotPrices: true);
            payment.OrderId = createdOrder.Id;
            createdOrderToNotify = createdOrder;
        }

        payment.Status = PaymentStatuses.Paid;
        payment.CompletedUtc = paidUtc;
        payment.FailedUtc = null;
        await _context.SaveChangesAsync(cancellationToken);

        if (createdOrderToNotify != null)
        {
            try
            {
                await _notificationService.SendOrderNotificationAsync(createdOrderToNotify, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Payment {PaymentId} completed, but notifications for order {OrderId} failed.",
                    payment.Id,
                    createdOrderToNotify.Id);
            }
        }
    }

    private async Task<Payment?> FindPaymentAsync(
        string provider,
        GatewayPaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        if (paymentEvent.PaymentId is Guid paymentId)
        {
            var byId = await _context.Payments.FirstOrDefaultAsync(
                payment => payment.Id == paymentId && payment.Provider == provider,
                cancellationToken);
            if (byId != null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentEvent.ProviderCheckoutId))
        {
            var byCheckout = await _context.Payments.FirstOrDefaultAsync(
                payment => payment.Provider == provider &&
                           payment.ProviderCheckoutId == paymentEvent.ProviderCheckoutId,
                cancellationToken);
            if (byCheckout != null)
            {
                return byCheckout;
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentEvent.ProviderPaymentId))
        {
            return await _context.Payments.FirstOrDefaultAsync(
                payment => payment.Provider == provider &&
                           payment.ProviderPaymentId == paymentEvent.ProviderPaymentId,
                cancellationToken);
        }

        return null;
    }

    private async Task<List<GatewayLineItem>> BuildLineItemsFromRequestAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var menuIds = request.OrderItems
            .Select(item => item.MenuItemId)
            .Concat(request.OrderItems.SelectMany(item => item.AddOns.Select(addOn => addOn.MenuItemId)))
            .Distinct()
            .ToList();
        var menuLookup = await _context.MenuItems
            .Where(item => menuIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var lineItems = new List<GatewayLineItem>();
        foreach (var item in request.OrderItems)
        {
            if (!menuLookup.TryGetValue(item.MenuItemId, out var menuItem))
            {
                throw new InvalidOperationException($"Menu item {item.MenuItemId} not found.");
            }

            item.UnitPrice = menuItem.EffectivePrice;
            lineItems.Add(new GatewayLineItem(
                menuItem.Name,
                menuItem.Description,
                item.UnitPrice.Value,
                item.Quantity));

            foreach (var addOn in item.AddOns)
            {
                if (!menuLookup.TryGetValue(addOn.MenuItemId, out var addOnItem))
                {
                    throw new InvalidOperationException($"Add-on item {addOn.MenuItemId} not found.");
                }

                addOn.UnitPrice = addOnItem.EffectivePrice;
                lineItems.Add(new GatewayLineItem(
                    $"{menuItem.Name} add-on: {addOnItem.Name}",
                    addOnItem.Description,
                    addOn.UnitPrice.Value,
                    addOn.Quantity));
            }
        }

        return lineItems;
    }

    private static List<GatewayLineItem> BuildLineItemsFromOrder(Order order)
    {
        var lineItems = new List<GatewayLineItem>();
        foreach (var item in order.OrderItems)
        {
            var menuItem = item.MenuItem
                ?? throw new InvalidOperationException($"Order item {item.Id} is missing menu data.");
            lineItems.Add(new GatewayLineItem(
                menuItem.Name,
                menuItem.Description,
                item.UnitPrice,
                item.Quantity));

            foreach (var addOn in item.AddOns ?? [])
            {
                var addOnItem = addOn.MenuItem
                    ?? throw new InvalidOperationException($"Add-on {addOn.Id} is missing menu data.");
                lineItems.Add(new GatewayLineItem(
                    $"{menuItem.Name} add-on: {addOnItem.Name}",
                    addOnItem.Description,
                    addOn.UnitPrice,
                    addOn.Quantity));
            }
        }

        return lineItems;
    }

    private static void ValidateCustomerIdentity(CheckoutSessionRequest request, Order order)
    {
        var requestPhone = NormalizePhone(request.CustomerPhone ?? string.Empty);
        var orderPhone = NormalizePhone(order.CustomerPhone ?? string.Empty);
        if (!string.IsNullOrEmpty(orderPhone))
        {
            if (string.IsNullOrEmpty(requestPhone) || requestPhone != orderPhone)
            {
                throw new InvalidOperationException("Phone number does not match this order.");
            }
            return;
        }

        if (string.IsNullOrEmpty(NormalizeName(request.CustomerName)) ||
            NormalizeName(request.CustomerName) != NormalizeName(order.CustomerName))
        {
            throw new InvalidOperationException("Customer details do not match this order.");
        }
    }

    private IPaymentGateway GetGateway(string? providerName)
    {
        if (TryGetGateway(providerName, out var gateway))
        {
            return gateway;
        }

        var requested = string.IsNullOrWhiteSpace(providerName) ? DefaultProviderName : providerName.Trim();
        throw new PaymentProviderUnavailableException(
            $"Payment provider '{requested}' is not available.");
    }

    private bool TryGetGateway(string? providerName, out IPaymentGateway gateway)
    {
        var requested = string.IsNullOrWhiteSpace(providerName) ? DefaultProviderName : providerName.Trim();
        return _gateways.TryGetValue(requested, out gateway!);
    }

    private static string NormalizePhone(string phone) =>
        new(phone.Where(char.IsDigit).ToArray());

    private static string NormalizeName(string name) =>
        string.Join(
            " ",
            (name ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private sealed record PreparedCheckout(
        CheckoutSessionRequest Payload,
        IReadOnlyList<GatewayLineItem> LineItems,
        string SuccessUrl,
        string CancelUrl);
}

public sealed record PaymentCheckoutResult(
    string CheckoutUrl,
    string CheckoutId,
    string Provider);

public sealed class PaymentProviderUnavailableException(string message) : Exception(message);
