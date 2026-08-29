namespace CoffeeShopApi.Models;

public sealed record OrderTrackingUnavailableDto(string Code, string Message)
{
    public static OrderTrackingUnavailableDto Response { get; } = new(
        "order_status_unavailable",
        "This order status is no longer available.");
}
