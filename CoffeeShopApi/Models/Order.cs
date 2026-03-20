using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(100, MinimumLength = 1)]
        [Column("customername")]
        public required string CustomerName { get; set; }

        [StringLength(20)]
        [Column("customerphone")]
        public string? CustomerPhone { get; set; }

        [Column("orderdate")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column("orderstatus")]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Received;

        [Required]
        [MinLength(1, ErrorMessage = "At least one order item is required")]
        public required List<OrderItem> OrderItems { get; set; }
    }
}
