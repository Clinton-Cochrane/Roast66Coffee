// Models/MenuItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models
{
    [Table("menuitems")]
    public class MenuItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; }

        [Required]
        [Column("price")]
        public decimal Price { get; set; }

        [Column("description")]
        public string Description { get; set; }
    }
}
