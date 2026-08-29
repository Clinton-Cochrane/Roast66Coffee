using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models.Payments;

public class CheckoutSessionRequest
{
    /// <summary>
    /// Checkout only settles an order that was already created. Phone must match the
    /// order when present; otherwise the customer name must match. Line items and
    /// prices are always taken from the stored order.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? ExistingOrderId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [EmailAddress]
    [StringLength(320)]
    public string? CustomerEmail { get; set; }

    public bool CustomerNotificationOptIn { get; set; }
}
