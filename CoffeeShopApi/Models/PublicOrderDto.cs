namespace CoffeeShopApi.Models;

public sealed record PublicOrderDto(
    int Id,
    string CustomerName,
    DateTime OrderDate,
    OrderStatus OrderStatus,
    DateTime? PaidUtc,
    string TrackingToken,
    IReadOnlyList<PublicOrderItemDto> OrderItems)
{
    public static PublicOrderDto FromOrder(Order order) => new(
        order.Id,
        order.CustomerName,
        order.OrderDate,
        order.OrderStatus,
        order.PaidUtc,
        order.TrackingToken,
        (order.OrderItems ?? []).Select(PublicOrderItemDto.FromOrderItem).ToList());
}

public sealed record PublicOrderItemDto(
    int Quantity,
    string? Notes,
    PublicMenuItemDto? MenuItem,
    IReadOnlyList<PublicOrderAddOnDto> AddOns)
{
    public static PublicOrderItemDto FromOrderItem(OrderItem item) => new(
        item.Quantity,
        item.Notes,
        item.MenuItem is null ? null : PublicMenuItemDto.FromMenuItem(item.MenuItem, item.UnitPrice),
        (item.AddOns ?? []).Select(PublicOrderAddOnDto.FromAddOn).ToList());
}

public sealed record PublicOrderAddOnDto(int Quantity, PublicMenuItemDto? MenuItem)
{
    public static PublicOrderAddOnDto FromAddOn(AddOn addOn) => new(
        addOn.Quantity,
        addOn.MenuItem is null ? null : PublicMenuItemDto.FromMenuItem(addOn.MenuItem, addOn.UnitPrice));
}

public sealed record PublicMenuItemDto(string Name, decimal Price)
{
    public static PublicMenuItemDto FromMenuItem(MenuItem item, decimal unitPrice) => new(item.Name, unitPrice);
}
