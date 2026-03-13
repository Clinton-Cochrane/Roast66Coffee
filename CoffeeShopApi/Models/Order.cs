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

        [Required]
        [Column("customername")]
        public required string CustomerName { get; set; }

        [Column("customerphone")]
        public string? CustomerPhone { get; set; }

        [Column("orderdate")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Column("orderstatus")]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Received;

        [JsonPropertyName("OrderItems")]
        public required List<OrderItem> OrderItems { get; set; }
    }
}
