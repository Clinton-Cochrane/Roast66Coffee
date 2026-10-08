namespace CoffeeShopApi.Services.Payments;

/// <summary>
/// Provider-owned in-person collection, without checkout redirects or reader details.
/// Adapters must honor the persisted idempotency key and return Failed only for a
/// definitive failure. An uncertain/timeout result must throw, leaving payment pending.
/// Webhook parsing must verify authenticity before returning an authoritative event.
/// </summary>
public interface IInPersonPaymentGateway
{
    string ProviderName { get; }
    bool IsConfigured();

    Task<InPersonGatewayResult> StartPaymentAsync(
        InPersonGatewayRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<GatewayPaymentEvent?> ParseWebhookAsync(
        string body,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}

public sealed record InPersonGatewayRequest(
    Guid PaymentId,
    int OrderId,
    decimal Amount,
    string Currency,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record InPersonGatewayResult(
    GatewayPaymentStatus Status,
    string? ProviderPaymentId = null);
