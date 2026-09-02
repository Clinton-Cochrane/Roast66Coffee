// Services/MenuService.cs
using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CoffeeShopApi.Security;

namespace CoffeeShopApi.Services
{
    public enum HomepageSpecialSelectionResult
    {
        Updated,
        NotFound,
        Unavailable,
        LimitReached
    }

    public enum MenuItemUpdateResult { Updated, NotFound, InvalidPromotion }

    public sealed record MenuReplacementSummary(int PreviousItemCount, int NewItemCount);

    /// <summary>
    /// Maintains the live menu and its cross-row constraints. Order history does not
    /// depend on current menu values because <see cref="OrderService"/> snapshots them
    /// at submission time.
    /// </summary>
    public class MenuService(ApplicationDbContext context, AuditEventFactory? auditEvents = null)
    {
        public const int MaxHomepageSpecials = 3;
        // The limit spans rows, so row locks cannot prevent another writer from
        // selecting a different item after this request counts the current set.
        private const string HomepageSpecialLockSql =
            "LOCK TABLE menuitems IN SHARE ROW EXCLUSIVE MODE";
        private readonly ApplicationDbContext _context = context;
        private readonly AuditEventFactory? _auditEvents = auditEvents;

        public async Task<IEnumerable<MenuItem>> GetMenuItemsAsync()
        {
            return await _context.MenuItems
                .Where(item => !item.IsArchived)
                .ToListAsync();
        }

        public async Task<IEnumerable<MenuItem>> GetAllMenuItemsAsync()
        {
            return await _context.MenuItems.ToListAsync();
        }

        public async Task<MenuItem?> GetMenuItemByIdAsync(int id)
        {
            return await _context.MenuItems.FindAsync(id);
        }

        public async Task<MenuItem> CreateMenuItemAsync(MenuItem menuItem, StaffActor? actor = null)
        {
            menuItem.IsFeaturedOnHome = false;
            menuItem.IsArchived = false;
            await using var transaction =
                actor != null && _auditEvents != null && _context.Database.IsRelational()
                    ? await _context.Database.BeginTransactionAsync()
                    : null;
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();
            AddAudit(actor, "menu.created", menuItem.Id, new { menuItem.Name });
            if (actor != null && _auditEvents != null)
            {
                await _context.SaveChangesAsync();
            }
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
            return menuItem;
        }

        public async Task<bool> UpdateMenuItemAsync(MenuItem menuItem, StaffActor? actor = null)
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
            AddAudit(actor, "menu.updated", existingItem.Id, new { existingItem.Name });

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

        /// <summary>
        /// Atomically enforces the global homepage-special limit. PostgreSQL takes a
        /// table lock because locking one row cannot serialize writers selecting
        /// different rows; EF InMemory relies on the surrounding single-process test.
        /// </summary>
        public async Task<HomepageSpecialSelectionResult> SetHomepageSpecialAsync(
            int id,
            bool isSelected,
            CancellationToken cancellationToken = default,
            StaffActor? actor = null)
        {
            IDbContextTransaction? ownedTransaction = null;
            try
            {
                if (_context.Database.IsNpgsql())
                {
                    if (_context.Database.CurrentTransaction == null)
                    {
                        ownedTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                    }

                    await _context.Database.ExecuteSqlRawAsync(
                        HomepageSpecialLockSql,
                        cancellationToken);
                }

                var menuItem = await _context.MenuItems.FindAsync([id], cancellationToken);
                HomepageSpecialSelectionResult result;
                if (menuItem == null)
                {
                    result = HomepageSpecialSelectionResult.NotFound;
                }
                else if (menuItem.IsArchived && isSelected)
                {
                    result = HomepageSpecialSelectionResult.Unavailable;
                }
                else if (menuItem.IsFeaturedOnHome == isSelected)
                {
                    result = HomepageSpecialSelectionResult.Updated;
                }
                else if (isSelected && await _context.MenuItems.CountAsync(
                             item => !item.IsArchived && item.IsFeaturedOnHome,
                             cancellationToken) >= MaxHomepageSpecials)
                {
                    result = HomepageSpecialSelectionResult.LimitReached;
                }
                else
                {
                    menuItem.IsFeaturedOnHome = isSelected;
                    AddAudit(actor, "menu.homepage_special.changed", id, new { IsSelected = isSelected });
                    await _context.SaveChangesAsync(cancellationToken);
                    result = HomepageSpecialSelectionResult.Updated;
                }

                if (ownedTransaction != null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch
            {
                if (ownedTransaction != null)
                {
                    await ownedTransaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (ownedTransaction != null)
                {
                    await ownedTransaction.DisposeAsync();
                }
            }
        }

        public async Task<bool> SetMenuSpecialAsync(int id, bool isSelected, StaffActor? actor = null)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null) return false;
            var nextCategory = isSelected ? CategoryType.SPECIALS : CategoryType.DRINKS;
            if (item.CategoryType == nextCategory) return true;
            item.CategoryType = nextCategory;
            AddAudit(actor, "menu.menu_special.changed", id, new { IsSelected = isSelected });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<MenuItemUpdateResult> SetPromotionAsync(int id, string? promotion, StaffActor? actor = null)
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
            AddAudit(actor, "menu.promotion.changed", id, new { Promotion = promotion?.Trim() });
            await _context.SaveChangesAsync();
            return MenuItemUpdateResult.Updated;
        }

        /// <summary>
        /// Accepts only invariant-culture <c>$amount</c> or <c>percent%</c> notation and
        /// rejects discounts that would reduce the effective price below one cent.
        /// </summary>
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

        public async Task<bool> DeleteMenuItemAsync(int id, StaffActor? actor = null)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return false;
            }

            _context.MenuItems.Remove(menuItem);
            AddAudit(actor, "menu.deleted", id, new { menuItem.Name });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ArchiveMenuItemAsync(int id, StaffActor? actor = null)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return false;
            }

            menuItem.IsArchived = true;
            menuItem.IsFeaturedOnHome = false;
            AddAudit(actor, "menu.archived", id, new { menuItem.Name });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RestoreMenuItemAsync(int id, StaffActor? actor = null)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return false;
            }

            menuItem.IsArchived = false;
            AddAudit(actor, "menu.restored", id, new { menuItem.Name });
            await _context.SaveChangesAsync();
            return true;
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }

        /// <summary>
        /// Replaces all menu items with a validated import. Client-provided IDs are
        /// ignored so imported rows cannot collide with database identity values.
        /// The delete/insert set is transactional and shares the homepage-special lock.
        /// </summary>
        public async Task<MenuReplacementSummary> BulkReplaceAsync(
            IEnumerable<MenuItem> menuItems,
            CancellationToken cancellationToken = default,
            StaffActor? actor = null,
            string auditAction = "menu.imported")
        {
            var items = menuItems.Select(m =>
            {
                return new MenuItem
                {
                    Name = m.Name,
                    Price = m.Price,
                    Description = m.Description ?? string.Empty,
                    CategoryType = m.CategoryType,
                    IsFeaturedOnHome = m.IsFeaturedOnHome,
                    IsArchived = m.IsArchived,
                    PromotionType = m.PromotionType,
                    PromotionValue = m.PromotionValue
                };
            }).ToList();

            if (items.Count == 0)
            {
                throw new ArgumentException("Menu must contain at least one item.", nameof(menuItems));
            }

            ValidateReplacement(items);

            IDbContextTransaction? ownedTransaction = null;
            try
            {
                if (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
                {
                    ownedTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                }
                if (_context.Database.IsNpgsql())
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        HomepageSpecialLockSql,
                        cancellationToken);
                }

                var existingItems = await _context.MenuItems.ToListAsync(cancellationToken);
                _context.MenuItems.RemoveRange(existingItems);
                await _context.MenuItems.AddRangeAsync(items, cancellationToken);
                if (actor != null && _auditEvents != null)
                {
                    _auditEvents.Add(
                        actor,
                        auditAction,
                        "menu",
                        "all",
                        new { PreviousItemCount = existingItems.Count, NewItemCount = items.Count });
                }
                await _context.SaveChangesAsync(cancellationToken);
                if (ownedTransaction != null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }

                return new MenuReplacementSummary(existingItems.Count, items.Count);
            }
            catch
            {
                if (ownedTransaction != null)
                {
                    await ownedTransaction.RollbackAsync(cancellationToken);
                }
                throw;
            }
            finally
            {
                if (ownedTransaction != null)
                {
                    await ownedTransaction.DisposeAsync();
                }
            }
        }

        public static void ValidateReplacement(IReadOnlyCollection<MenuItem> menuItems)
        {
            var errors = new List<string>();

            foreach (var item in menuItems)
            {
                var itemErrors = new List<ValidationResult>();
                if (!Validator.TryValidateObject(
                        item,
                        new ValidationContext(item),
                        itemErrors,
                        validateAllProperties: true))
                {
                    errors.AddRange(itemErrors.Select(error => error.ErrorMessage ?? "Invalid menu item."));
                }

                if (!Enum.IsDefined(item.CategoryType))
                {
                    errors.Add($"Menu item '{item.Name}' has an invalid category.");
                }

                if (item.IsArchived && item.IsFeaturedOnHome)
                {
                    errors.Add($"Archived menu item '{item.Name}' cannot be featured on the homepage.");
                }

                if (item.PromotionType.HasValue != item.PromotionValue.HasValue ||
                    (item.PromotionType.HasValue && !Enum.IsDefined(item.PromotionType.Value)) ||
                    (item.PromotionValue.HasValue &&
                     (item.PromotionValue.Value <= 0 || item.EffectivePrice < 0.01m)))
                {
                    errors.Add($"Menu item '{item.Name}' has an invalid promotion.");
                }
            }

            var duplicateNames = menuItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateNames.Count > 0)
            {
                errors.Add($"Menu item names must be unique: {string.Join(", ", duplicateNames)}.");
            }

            if (menuItems.Count(item => item.IsFeaturedOnHome) > MaxHomepageSpecials)
            {
                errors.Add($"Only {MaxHomepageSpecials} homepage specials can be selected.");
            }

            if (errors.Count > 0)
            {
                throw new ValidationException(string.Join(" ", errors.Distinct()));
            }
        }

        private void AddAudit(StaffActor? actor, string action, int id, object details)
        {
            if (actor == null || _auditEvents == null) return;
            _auditEvents.Add(
                actor,
                action,
                "menu_item",
                id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                details);
        }
    }
}
