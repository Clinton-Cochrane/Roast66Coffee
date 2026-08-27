using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models.Payments;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(50)]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [StringLength(50)]
    [Column("method")]
    public string? Method { get; set; }

    [Required]
    [StringLength(24)]
    [Column("status")]
    public string Status { get; set; } = PaymentStatuses.Pending;

    [Column("amount", TypeName = "numeric(10,2)")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column("providercheckoutid")]
    public string ProviderCheckoutId { get; set; } = string.Empty;

    [Column("providerpaymentid")]
    public string? ProviderPaymentId { get; set; }

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

    [Column("createdutc")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Column("completedutc")]
    public DateTime? CompletedUtc { get; set; }

    [Column("failedutc")]
    public DateTime? FailedUtc { get; set; }

    [Column("refundedutc")]
    public DateTime? RefundedUtc { get; set; }

    [Column("confirmedbystaffutc")]
    public DateTime? ConfirmedByStaffUtc { get; set; }

    [Column("orderid")]
    public int? OrderId { get; set; }

    public Order? Order { get; set; }
}

public static class PaymentStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
    public const string Refunded = "refunded";
}
