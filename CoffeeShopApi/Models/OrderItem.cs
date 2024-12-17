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

        [InverseProperty("OrderItem")]

        public List<AddOn>? AddOns { get; set; }
    }

    [Table("addons")]
    public class AddOn
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("menuitemid")]
        public int MenuItemId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        // Foreign key to link back to the order item
        [Required]
        [Column("orderitemid")]
        public int OrderItemId { get; set; }

        [InverseProperty("AddOns")]
        [ForeignKey("OrderItemId")]
        public OrderItem? OrderItem { get; set; }

        [ForeignKey("MenuItemId")]
        public MenuItem? MenuItem { get; set; }
    }
}