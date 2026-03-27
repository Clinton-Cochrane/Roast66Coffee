using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models.Payments;

public class CheckoutSessionRequest
{
    /// <summary>
    /// When set, Checkout charges this existing order (prepay) instead of creating a new order after payment.
    /// Phone must match the order; line items are taken from the stored order.
    /// </summary>
    public int? ExistingOrderId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string CustomerPhone { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(320)]
    public string? CustomerEmail { get; set; }

    public bool CustomerNotificationOptIn { get; set; }

    /// <summary>Required for new orders; ignored when <see cref="ExistingOrderId"/> is set.</summary>
    public List<CheckoutOrderItemRequest> OrderItems { get; set; } = [];
}

public class CheckoutOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int MenuItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<CheckoutAddOnItemRequest> AddOns { get; set; } = [];
}

public class CheckoutAddOnItemRequest
{
    [Range(1, int.MaxValue)]
    public int MenuItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}
