using Stripe;
using Stripe.Checkout;

namespace CoffeeShopApi.Services.Payments;

public sealed class StripePaymentGateway(IConfiguration configuration) : IPaymentGateway
{
    public const string Name = "stripe";
    private const string PaymentIdMetadataKey = "roast66_payment_id";

    private readonly IConfiguration _configuration = configuration;

    public string ProviderName => Name;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["Stripe:SecretKey"]);

    public async Task<GatewayCheckoutResult> CreateCheckoutAsync(
        GatewayCheckoutRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, string>(request.Metadata)
        {
            [PaymentIdMetadataKey] = request.PaymentId.ToString("D")
        };

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            LineItems = request.LineItems.Select(ToStripeLineItem).ToList(),
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata
            }
        };

        var session = await CreateSessionService().CreateAsync(
            options,
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        return new GatewayCheckoutResult(
            session.Url ?? string.Empty,
            session.Id,
            session.PaymentIntentId);
    }

    public async Task<GatewayCheckoutResult> GetCheckoutAsync(
        string providerCheckoutId,
        CancellationToken cancellationToken = default)
    {
        var session = await CreateSessionService().GetAsync(
            providerCheckoutId,
            cancellationToken: cancellationToken);

        return new GatewayCheckoutResult(
            session.Url ?? string.Empty,
            session.Id,
            session.PaymentIntentId);
    }

    public Task<GatewayPaymentEvent?> ParseWebhookAsync(
        string body,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            throw new PaymentProviderUnavailableException(
                "Stripe webhook verification is not configured.");
        }

        if (!headers.TryGetValue("Stripe-Signature", out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            throw new PaymentWebhookException("Stripe webhook signature is missing.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(body, signature, webhookSecret);
        }
        catch (Exception ex)
        {
            throw new PaymentWebhookException("Stripe webhook signature is invalid.", ex);
        }

        GatewayPaymentEvent? paymentEvent = stripeEvent.Type switch
        {
            "checkout.session.completed" => FromCheckoutSession(stripeEvent, requirePaid: true),
            "checkout.session.async_payment_succeeded" => FromCheckoutSession(stripeEvent, requirePaid: false),
            "checkout.session.async_payment_failed" => FromCheckoutSession(stripeEvent, requirePaid: false, failed: true),
            "payment_intent.payment_failed" => FromPaymentIntent(stripeEvent),
            _ => null
        };

        return Task.FromResult(paymentEvent);
    }

    private SessionService CreateSessionService()
    {
        var secretKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe is not configured for this environment.");
        }

        return new SessionService(new StripeClient(secretKey));
    }

    private static SessionLineItemOptions ToStripeLineItem(GatewayLineItem item) =>
        new()
        {
            Quantity = item.Quantity,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmount = (long)Math.Round(item.UnitPrice * 100M),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = item.Name,
                    Description = item.Description
                }
            }
        };

    private static GatewayPaymentEvent? FromCheckoutSession(
        Event stripeEvent,
        bool requirePaid,
        bool failed = false)
    {
        if (stripeEvent.Data.Object is not Session session)
        {
            return null;
        }

        var status = failed
            ? GatewayPaymentStatus.Failed
            : requirePaid && !string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                ? GatewayPaymentStatus.Pending
                : GatewayPaymentStatus.Paid;

        return new GatewayPaymentEvent(
            ReadPaymentId(session.Metadata),
            session.Id,
            session.PaymentIntentId,
            status);
    }

    private static GatewayPaymentEvent? FromPaymentIntent(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not PaymentIntent intent)
        {
            return null;
        }

        return new GatewayPaymentEvent(
            ReadPaymentId(intent.Metadata),
            null,
            intent.Id,
            GatewayPaymentStatus.Failed);
    }

    private static Guid? ReadPaymentId(IDictionary<string, string>? metadata) =>
        metadata != null &&
        metadata.TryGetValue(PaymentIdMetadataKey, out var raw) &&
        Guid.TryParse(raw, out var paymentId)
            ? paymentId
            : null;
}
