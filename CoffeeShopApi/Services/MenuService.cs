// Services/MenuService.cs
using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services
{
    public enum HomepageSpecialSelectionResult
    {
        Updated,
        NotFound,
        LimitReached
    }

    public enum MenuItemUpdateResult { Updated, NotFound, InvalidPromotion }

    public class MenuService(ApplicationDbContext context)
    {
        public const int MaxHomepageSpecials = 3;
        private readonly ApplicationDbContext _context = context;

        public async Task<IEnumerable<MenuItem>> GetMenuItemsAsync()
        {
            return await _context.MenuItems.ToListAsync();
        }

        public async Task<MenuItem?> GetMenuItemByIdAsync(int id)
        {
            return await _context.MenuItems.FindAsync(id);
        }

        public async Task<MenuItem> CreateMenuItemAsync(MenuItem menuItem)
        {
            menuItem.IsFeaturedOnHome = false;
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();
            return menuItem;
        }

        public async Task<bool> UpdateMenuItemAsync(MenuItem menuItem)
        {
            var existingItem = await _context.MenuItems.FindAsync(menuItem.Id);
            if (existingItem == null)
            {
                return false;
            }

            existingItem.Name = menuItem.Name;
            existingItem.Price = menuItem.Price;
            existingItem.Description = menuItem.Description;
            existingItem.CategoryType = menuItem.CategoryType;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MenuItemExists(menuItem.Id))
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<HomepageSpecialSelectionResult> SetHomepageSpecialAsync(int id, bool isSelected)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return HomepageSpecialSelectionResult.NotFound;
            }

            if (menuItem.IsFeaturedOnHome == isSelected)
            {
                return HomepageSpecialSelectionResult.Updated;
            }

            if (isSelected && await _context.MenuItems.CountAsync(item => item.IsFeaturedOnHome) >= MaxHomepageSpecials)
            {
                return HomepageSpecialSelectionResult.LimitReached;
            }

            menuItem.IsFeaturedOnHome = isSelected;
            await _context.SaveChangesAsync();
            return HomepageSpecialSelectionResult.Updated;
        }

        public async Task<bool> SetMenuSpecialAsync(int id, bool isSelected)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return false;
            item.CategoryType = isSelected ? CategoryType.SPECIALS : CategoryType.DRINKS;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<MenuItemUpdateResult> SetPromotionAsync(int id, string? promotion)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return MenuItemUpdateResult.NotFound;
            if (string.IsNullOrWhiteSpace(promotion))
            {
                item.PromotionType = null;
                item.PromotionValue = null;
            }
            else if (!TryParsePromotion(promotion, item.Price, out var type, out var value))
            {
                return MenuItemUpdateResult.InvalidPromotion;
            }
            else
            {
                item.PromotionType = type;
                item.PromotionValue = value;
            }
            await _context.SaveChangesAsync();
            return MenuItemUpdateResult.Updated;
        }

        public static bool TryParsePromotion(string input, decimal price, out PromotionType type, out decimal value)
        {
            type = default;
            value = 0;
            var text = input.Trim();
            if (text.Length < 2) return false;
            var isDollar = text[0] == '$';
            var isPercent = text[^1] == '%';
            if (isDollar == isPercent) return false;
            var number = isDollar ? text[1..] : text[..^1];
            if (!decimal.TryParse(number, System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture, out value) || value <= 0) return false;
            type = isDollar ? PromotionType.Dollar : PromotionType.Percentage;
            return MenuItem.CalculateEffectivePrice(price, type, value) >= 0.01m;
        }

        public async Task<bool> DeleteMenuItemAsync(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return false;
            }

            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();
            return true;
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }

        /// <summary>
        /// Replaces all menu items with the provided list. Used for bulk import.
        /// Ignores client-provided IDs; new items get fresh IDs.
        /// </summary>
        public async Task BulkReplaceAsync(IEnumerable<MenuItem> menuItems)
        {
            var selectedCount = 0;
            var items = menuItems.Select(m =>
            {
                var isSelected = m.IsFeaturedOnHome && selectedCount < MaxHomepageSpecials;
                if (isSelected) selectedCount++;

                return new MenuItem
                {
                    Name = m.Name,
                    Price = m.Price,
                    Description = m.Description ?? string.Empty,
                    CategoryType = m.CategoryType,
                    IsFeaturedOnHome = isSelected
                    ,PromotionType = m.PromotionType
                    ,PromotionValue = m.PromotionValue
                };
            }).ToList();
            _context.MenuItems.RemoveRange(_context.MenuItems);
            await _context.MenuItems.AddRangeAsync(items);
            await _context.SaveChangesAsync();
        }
    }
}
