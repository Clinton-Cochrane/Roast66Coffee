// Models/MenuItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models
{
   public enum PromotionType
   {
       Dollar = 1,
       Percentage = 2
   }

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

        [Column("is_featured_on_home")]
        public bool IsFeaturedOnHome { get; set; }

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("promotion_type")]
        public PromotionType? PromotionType { get; set; }

        [Column("promotion_value", TypeName = "numeric(10,2)")]
        public decimal? PromotionValue { get; set; }

        [NotMapped]
        public decimal EffectivePrice => CalculateEffectivePrice(Price, PromotionType, PromotionValue);

        [NotMapped]
        public string? Promotion => PromotionType switch
        {
            Models.PromotionType.Dollar when PromotionValue.HasValue => $"${PromotionValue.Value:0.##}",
            Models.PromotionType.Percentage when PromotionValue.HasValue => $"{PromotionValue.Value:0.##}%",
            _ => null
        };

        public static decimal CalculateEffectivePrice(decimal price, PromotionType? type, decimal? value)
        {
            if (type is null || value is null) return price;
            var discounted = type == Models.PromotionType.Dollar
                ? price - value.Value
                : price * (1m - value.Value / 100m);
            return Math.Round(discounted, 2, MidpointRounding.AwayFromZero);
        }

    }
}
