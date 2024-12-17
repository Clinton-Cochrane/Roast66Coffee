using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CoffeeShopApi.Models
{
    [Table("orders")]
    public class Order
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("customername")]
        public required string CustomerName { get; set; }

        [Column("orderdate")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("OrderItems")]
        public required List<OrderItem> OrderItems { get; set; }
        public bool Status { get; internal set; } = false;
    }
}
