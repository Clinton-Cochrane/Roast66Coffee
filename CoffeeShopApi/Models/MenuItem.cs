// Models/MenuItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models
{
   public enum CategoryType
{
    COFFEE,     // 0
    SPECIALS,   // 1
    FLAVORS,    // 2
    DRINKS      // 3
}


    [Table("menuitems")]
    public class MenuItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("price")]
        public decimal Price { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public CategoryType CategoryType { get; set; }
    }
}
