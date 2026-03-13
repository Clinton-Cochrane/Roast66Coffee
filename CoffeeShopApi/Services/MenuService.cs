// Services/MenuService.cs
using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services
{
    public class MenuService(ApplicationDbContext context)
    {
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
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();
            return menuItem;
        }

        public async Task<bool> UpdateMenuItemAsync(MenuItem menuItem)
        {
            _context.Entry(menuItem).State = EntityState.Modified;

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
            var items = menuItems.Select(m => new MenuItem
            {
                Name = m.Name,
                Price = m.Price,
                Description = m.Description ?? string.Empty,
                CategoryType = m.CategoryType
            }).ToList();
            _context.MenuItems.RemoveRange(_context.MenuItems);
            await _context.MenuItems.AddRangeAsync(items);
            await _context.SaveChangesAsync();
        }
    }
}
