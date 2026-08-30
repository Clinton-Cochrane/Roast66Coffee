using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CoffeeShopApi.Services;

public class OrderService(ApplicationDbContext context, IConfiguration configuration)
{
    public const decimal MaxOrderValue = 500m;

    private readonly ApplicationDbContext _context = context;
    private readonly IConfiguration _configuration = configuration;

    private int DuplicateDetectionWindowMinutes =>
        _configuration.GetValue("Order:DuplicateDetectionWindowMinutes", 2);

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns!)
            .ThenInclude(a => a.MenuItem)
            .ToListAsync();
    }


    public async Task<Order?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns!)
            .ThenInclude(a => a.MenuItem)
            .OrderBy(o => o.OrderDate)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetOrderByTrackingTokenAsync(
        string trackingToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackingToken) || trackingToken.Length != 43)
        {
            return null;
        }

        return await _context.Orders
            .Include(o => o.OrderItems!)
                .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems!)
                .ThenInclude(oi => oi.AddOns!)
                .ThenInclude(a => a.MenuItem)
            .FirstOrDefaultAsync(o => o.TrackingToken == trackingToken, cancellationToken);
    }

    /// <summary>
    /// Finds a duplicate order: same customer + same content within the configured time window.
    /// Returns the existing order if found, null otherwise.
    /// </summary>
    public async Task<Order?> FindDuplicateOrderAsync(Order order)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-DuplicateDetectionWindowMinutes);
        var customerKey = NormalizeCustomerKey(order);
        if (string.IsNullOrEmpty(customerKey)) return null;

        var incomingFingerprint = ComputeOrderContentFingerprint(order);
        if (string.IsNullOrEmpty(incomingFingerprint)) return null;

        var recentOrders = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns)
            .Where(o => o.OrderDate >= windowStart)
            .ToListAsync();

        var sameCustomer = recentOrders.Where(o => NormalizeCustomerKey(o) == customerKey);

        foreach (var existing in sameCustomer)
        {
            if (ComputeOrderContentFingerprint(existing) == incomingFingerprint)
                return existing;
        }
        return null;
    }

    private static string NormalizeCustomerKey(Order order)
    {
        var phone = NormalizePhone(order.CustomerPhone ?? "");
        if (!string.IsNullOrEmpty(phone)) return $"phone:{phone}";
        var name = (order.CustomerName ?? "").Trim();
        return string.IsNullOrEmpty(name) ? "" : $"name:{name.ToLowerInvariant()}";
    }

    private static string ComputeOrderContentFingerprint(Order order)
    {
        if (order.OrderItems == null || order.OrderItems.Count == 0)
            return string.Empty;

        var parts = order.OrderItems
            .OrderBy(oi => oi.MenuItemId)
            .Select(oi =>
            {
                var addons = (oi.AddOns ?? [])
                    .OrderBy(a => a.MenuItemId)
                    .Select(a => $"{a.MenuItemId}:{a.Quantity}");
                return $"{oi.MenuItemId}:{oi.Quantity}:{oi.Notes ?? ""}:{string.Join(",", addons)}";
            });
        return string.Join("|", parts);
    }

    public async Task<OrderSubmissionResult> SubmitOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        var requestedItems = (request.OrderItems ?? [])
            .OfType<CreateOrderItemRequest>()
            .ToList();
        var requestedIds = requestedItems.Select(item => item.MenuItemId)
            .Concat(requestedItems
                .SelectMany(item => item.AddOns ?? [])
                .OfType<CreateOrderAddOnRequest>()
                .Select(addOn => addOn.MenuItemId))
            .Distinct()
            .ToList();
        var menuItems = await _context.MenuItems
            .Where(item => requestedIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        decimal total = 0;
        for (var itemIndex = 0; itemIndex < requestedItems.Count; itemIndex++)
        {
            var requestedItem = requestedItems[itemIndex];
            var itemPath = $"orderItems[{itemIndex}].menuItemId";
            if (!TryGetAvailableMenuItem(menuItems, requestedItem.MenuItemId, out var menuItem))
            {
                errors[itemPath] = ["The selected menu item is unavailable."];
            }
            else if (!IsPrimaryCategory(menuItem.CategoryType))
            {
                errors[itemPath] = ["Primary order items must be coffee, drinks, or specials."];
            }
            else
            {
                total += menuItem.EffectivePrice * requestedItem.Quantity;
            }

            var requestedAddOns = (requestedItem.AddOns ?? [])
                .OfType<CreateOrderAddOnRequest>()
                .ToList();
            for (var addOnIndex = 0; addOnIndex < requestedAddOns.Count; addOnIndex++)
            {
                var requestedAddOn = requestedAddOns[addOnIndex];
                var addOnPath = $"orderItems[{itemIndex}].addOns[{addOnIndex}].menuItemId";
                if (!TryGetAvailableMenuItem(menuItems, requestedAddOn.MenuItemId, out var addOnMenuItem))
                {
                    errors[addOnPath] = ["The selected flavor is unavailable."];
                }
                else if (addOnMenuItem.CategoryType != CategoryType.FLAVORS)
                {
                    errors[addOnPath] = ["Add-ons must be flavors."];
                }
                else
                {
                    total += addOnMenuItem.EffectivePrice * requestedAddOn.Quantity;
                }
            }
        }

        if (total > MaxOrderValue)
        {
            errors["orderItems"] = [$"The server-calculated order total cannot exceed ${MaxOrderValue:0.00}."];
        }

        if (errors.Count > 0)
        {
            return new OrderSubmissionResult(OrderSubmissionStatus.Invalid, Errors: errors);
        }

        var order = new Order
        {
            CustomerName = request.CustomerName!.Trim(),
            CustomerPhone = null,
            CustomerEmail = null,
            CustomerNotificationOptIn = false,
            OrderDate = DateTime.UtcNow,
            OrderStatus = OrderStatus.Received,
            PaidUtc = null,
            PaymentProvider = null,
            PaymentReference = null,
            TrackingToken = GenerateTrackingToken(),
            OrderItems = requestedItems.Select(requestedItem =>
            {
                var menuItem = menuItems[requestedItem.MenuItemId];
                var orderItem = new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = requestedItem.Quantity,
                    Notes = NormalizeNotes(requestedItem.Notes),
                    AddOns = (requestedItem.AddOns ?? [])
                        .OfType<CreateOrderAddOnRequest>()
                        .Select(requestedAddOn =>
                    {
                        var addOnMenuItem = menuItems[requestedAddOn.MenuItemId];
                        var addOn = new AddOn
                        {
                            MenuItemId = addOnMenuItem.Id,
                            Quantity = requestedAddOn.Quantity
                        };
                        StampSnapshot(addOn, addOnMenuItem);
                        return addOn;
                    }).ToList()
                };
                StampSnapshot(orderItem, menuItem);
                return orderItem;
            }).ToList()
        };

        var duplicate = await FindDuplicateOrderAsync(order);
        if (duplicate != null)
        {
            return new OrderSubmissionResult(OrderSubmissionStatus.Duplicate, duplicate);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        var created = (await GetOrderByIdAsync(order.Id, cancellationToken))!;
        return new OrderSubmissionResult(OrderSubmissionStatus.Created, created);
    }

    private static bool TryGetAvailableMenuItem(
        IReadOnlyDictionary<int, MenuItem> menuItems,
        int id,
        out MenuItem menuItem)
    {
        if (menuItems.TryGetValue(id, out var found) &&
            !found.IsArchived &&
            Enum.IsDefined(found.CategoryType))
        {
            menuItem = found;
            return true;
        }

        menuItem = null!;
        return false;
    }

    private static bool IsPrimaryCategory(CategoryType category) =>
        category is CategoryType.COFFEE or CategoryType.DRINKS or CategoryType.SPECIALS;

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public async Task<Order> CreateOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = (order.OrderItems ?? []).Select(item => item.MenuItemId)
            .Concat((order.OrderItems ?? [])
                .SelectMany(item => item.AddOns ?? [])
                .Select(addOn => addOn.MenuItemId))
            .ToList();

        if (requestedIds.Count == 0 || requestedIds.Any(id => id is null))
        {
            throw new UnavailableMenuItemsException();
        }

        var ids = requestedIds.Select(id => id!.Value).Distinct().ToList();
        var menuItems = await _context.MenuItems
            .Where(item => ids.Contains(item.Id) && !item.IsArchived)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        if (menuItems.Count != ids.Count)
        {
            throw new UnavailableMenuItemsException();
        }

        foreach (var item in order.OrderItems ?? [])
        {
            var menuItem = menuItems[item.MenuItemId!.Value];
            StampSnapshot(item, menuItem);

            foreach (var addOn in item.AddOns ?? [])
            {
                var addOnMenuItem = menuItems[addOn.MenuItemId!.Value];
                StampSnapshot(addOn, addOnMenuItem);
            }
        }

        if (string.IsNullOrEmpty(order.TrackingToken))
        {
            order.TrackingToken = GenerateTrackingToken();
        }
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return (await GetOrderByIdAsync(order.Id, cancellationToken))!;
    }

    private static void StampSnapshot(OrderItem orderItem, MenuItem menuItem)
    {
        orderItem.ItemName = menuItem.Name;
        orderItem.ItemDescription = menuItem.Description;
        orderItem.ItemCategoryType = menuItem.CategoryType;
        orderItem.UnitPrice = menuItem.EffectivePrice;
    }

    private static void StampSnapshot(AddOn addOn, MenuItem menuItem)
    {
        addOn.ItemName = menuItem.Name;
        addOn.ItemDescription = menuItem.Description;
        addOn.ItemCategoryType = menuItem.CategoryType;
        addOn.UnitPrice = menuItem.EffectivePrice;
    }

    internal static string GenerateTrackingToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public async Task<bool> UpdateOrderAsync(Order order)
    {
        _context.Entry(order).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!OrderExists(order.Id))
            {
                return false;
            }
            else
            {
                throw;
            }
        }
    }

    private bool OrderExists(int id)
    {
        return _context.Orders.Any(e => e.Id == id);
    }

    internal async Task UpdateStatus(Order order, CancellationToken cancellationToken = default)
    {
        order.OrderStatus = order.OrderStatus switch
        {
            OrderStatus.Received => OrderStatus.Preparing,
            OrderStatus.Preparing => OrderStatus.ReadyForPickup,
            OrderStatus.ReadyForPickup => OrderStatus.Completed,
            OrderStatus.Completed => OrderStatus.Received,
            _ => OrderStatus.Received
        };
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Get order by ID and customer identity for public lookup.
    /// Accepts phone OR customer name, preferring phone when both are provided.
    /// </summary>
    public async Task<Order?> GetOrderForCustomerAsync(
        int orderId,
        string? phone,
        string? customerName,
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var normalizedPhone = NormalizePhone(phone);
            var orderPhone = NormalizePhone(order.CustomerPhone ?? "");
            return string.Equals(orderPhone, normalizedPhone, StringComparison.Ordinal) ? order : null;
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var requestedName = NormalizeName(customerName);
            var orderName = NormalizeName(order.CustomerName);
            return string.Equals(orderName, requestedName, StringComparison.Ordinal) ? order : null;
        }

        return null;
    }

    /// <summary>Count orders created after the given UTC timestamp. Used for admin badge.</summary>
    public async Task<int> GetCountSinceAsync(DateTime sinceUtc)
    {
        return await _context.Orders
            .Where(o => o.OrderDate > sinceUtc)
            .CountAsync();
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static string NormalizeName(string name) =>
        string.Join(
            " ",
            (name ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );
}
