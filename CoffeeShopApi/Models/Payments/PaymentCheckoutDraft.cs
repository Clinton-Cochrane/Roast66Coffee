using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models.Payments;

[Table("paymentcheckoutdrafts")]
public class PaymentCheckoutDraft
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("checkoutsessionid")]
    public string CheckoutSessionId { get; set; } = string.Empty;

    [Required]
    [Column("idempotencykey")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [Column("customername")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [Column("customerphone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required]
    [Column("payloadjson")]
    public string PayloadJson { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("createdutc")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Column("completedutc")]
    public DateTime? CompletedUtc { get; set; }

    [Column("failedutc")]
    public DateTime? FailedUtc { get; set; }

    [Column("stripepaymentintentid")]
    public string? StripePaymentIntentId { get; set; }

    [Column("orderid")]
    public int? OrderId { get; set; }
}
