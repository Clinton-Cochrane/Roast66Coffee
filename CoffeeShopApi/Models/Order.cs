using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CoffeeShopApi.Models
{
    public enum OrderStatus
    {
        Received = 0,
        Preparing = 1,
        ReadyForPickup = 2,
        Completed = 3
    }

    [Table("orders")]
    public class Order
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("trackingtoken")]
        [JsonIgnore]
        public string TrackingToken { get; set; } = string.Empty;

        [StringLength(128)]
        [Column("idempotencykey")]
        [JsonIgnore]
        public string? IdempotencyKey { get; set; }

        [StringLength(64)]
        [Column("requestfingerprint")]
        [JsonIgnore]
        public string? RequestFingerprint { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(100, MinimumLength = 1)]
        [Column("customername")]
        public required string CustomerName { get; set; }

        [StringLength(20)]
        [Column("customerphone")]
        public string? CustomerPhone { get; set; }

        [StringLength(320)]
        [Column("customeremail")]
        public string? CustomerEmail { get; set; }

        [Column("customernotificationoptin")]
        public bool CustomerNotificationOptIn { get; set; }

        [Column("orderdate")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column("orderstatus")]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Received;

        /// <summary>When this order most recently transitioned to Completed.</summary>
        [Column("completedutc")]
        public DateTime? CompletedUtc { get; set; }

        /// <summary>When set, this order has a confirmed payment.</summary>
        [Column("paidutc")]
        public DateTime? PaidUtc { get; set; }

        [StringLength(50)]
        [Column("paymentprovider")]
        public string? PaymentProvider { get; set; }

        [StringLength(255)]
        [Column("paymentreference")]
        public string? PaymentReference { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one order item is required")]
        public required List<OrderItem> OrderItems { get; set; }
    }
}
