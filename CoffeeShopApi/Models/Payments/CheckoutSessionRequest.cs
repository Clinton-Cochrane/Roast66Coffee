namespace CoffeeShopApi.Models.Payments;

public class CheckoutSessionRequest
{
    /// <summary>
    /// Checkout only settles an order that was already created. The tracking token
    /// proves possession of the private customer tracking link. Line items, prices,
    /// and customer details are always taken from the stored order.
    /// </summary>
    public int? ExistingOrderId { get; set; }

    // Deliberately unannotated so the controller can preserve its provider-config
    // response before ownership validation.
    public string? TrackingToken { get; set; }
}
