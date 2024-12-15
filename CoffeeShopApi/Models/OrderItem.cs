using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace CoffeeShopApi.Models
{
    [Table("orderitems")]
    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("orderid")]
        public int OrderId { get; set; }

        [Required]
        [Column("menuitemid")]
        public int MenuItemId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("Customer_Notes")]
        public string? Notes { get; set; }

        [JsonIgnore]
        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [ForeignKey("MenuItemId")]
        public MenuItem? MenuItem { get; set; }
    }
}