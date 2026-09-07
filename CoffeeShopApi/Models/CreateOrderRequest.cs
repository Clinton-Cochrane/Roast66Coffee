using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models;

/// <summary>
/// Customer-controlled fields accepted by the public order-creation endpoint.
/// Server-owned order state is intentionally absent from this request contract.
/// </summary>
public sealed class CreateOrderRequest
{
    [Required(ErrorMessage = "Customer name is required")]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [StringLength(320)]
    public string? CustomerEmail { get; set; }

    public bool CustomerNotificationOptIn { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one order item is required")]
    public List<CreateOrderItemRequest> OrderItems { get; set; } = [];
}

public sealed class CreateOrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must be a valid menu item")]
    public int? MenuItemId { get; set; }

    [Range(1, 12, ErrorMessage = "Quantity must be between 1 and 12")]
    public int Quantity { get; set; }

    public string? Notes { get; set; }

    public List<CreateOrderAddOnRequest>? AddOns { get; set; }
}

public sealed class CreateOrderAddOnRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must be a valid menu item")]
    public int? MenuItemId { get; set; }

    [Range(1, 12, ErrorMessage = "Quantity must be between 1 and 12")]
    public int Quantity { get; set; }
}
