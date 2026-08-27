namespace CoffeeShopApi.Services.Payments;

public interface IPaymentGateway
{
    string ProviderName { get; }

    bool IsConfigured();

    Task<GatewayCheckoutResult> CreateCheckoutAsync(
        GatewayCheckoutRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<GatewayCheckoutResult> GetCheckoutAsync(
        string providerCheckoutId,
        CancellationToken cancellationToken = default);

    Task<GatewayPaymentEvent?> ParseWebhookAsync(
        string body,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}

public sealed record GatewayCheckoutRequest(
    Guid PaymentId,
    string SuccessUrl,
    string CancelUrl,
    IReadOnlyList<GatewayLineItem> LineItems,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record GatewayLineItem(
    string Name,
    string? Description,
    decimal UnitPrice,
    int Quantity);

public sealed record GatewayCheckoutResult(
    string CheckoutUrl,
    string ProviderCheckoutId,
    string? ProviderPaymentId = null);

public sealed record GatewayPaymentEvent(
    Guid? PaymentId,
    string? ProviderCheckoutId,
    string? ProviderPaymentId,
    GatewayPaymentStatus Status,
    string? Method = null);

public enum GatewayPaymentStatus
{
    Pending,
    Paid,
    Failed
}

public sealed class PaymentWebhookException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PaymentWebhookRetryException(string message) : Exception(message);
