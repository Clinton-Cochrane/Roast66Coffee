using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services;

public class OrderService(ApplicationDbContext context, IConfiguration configuration)
{
    private readonly ApplicationDbContext _context = context;
    private readonly IConfiguration _configuration = configuration;

    private int DuplicateDetectionWindowMinutes =>
        _configuration.GetValue("Order:DuplicateDetectionWindowMinutes", 2);

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
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

    public async Task<Order> CreateOrderAsync(Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return (await GetOrderByIdAsync(order.Id))!;
    }

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

    public async Task<bool> DeleteOrderAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return false;
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return true;
    }

    private bool OrderExists(int id)
    {
        return _context.Orders.Any(e => e.Id == id);
    }

    internal async Task UpdateStatus(Order order)
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
        await _context.SaveChangesAsync();
    }

    /// <summary>Get order by ID and phone for customer lookup. Returns null if phone does not match.</summary>
    public async Task<Order?> GetOrderForCustomerAsync(int orderId, string phone, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderByIdAsync(orderId, cancellationToken);
        if (order == null) return null;
        var normalizedPhone = NormalizePhone(phone);
        var orderPhone = NormalizePhone(order.CustomerPhone ?? "");
        return string.Equals(orderPhone, normalizedPhone, StringComparison.Ordinal) ? order : null;
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
}
