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

        [Required(ErrorMessage = "Name is required")]
        [StringLength(200, MinimumLength = 1)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0, 999.99, ErrorMessage = "Price must be between 0 and 999.99")]
        [Column("price")]
        public decimal Price { get; set; }

        [StringLength(500)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public CategoryType CategoryType { get; set; }
    }
}
