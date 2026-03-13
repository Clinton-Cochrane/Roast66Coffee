using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services;

public class OrderService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
            .ToListAsync();
    }


    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns!)
            .ThenInclude(a => a.MenuItem)
            .OrderBy(o => o.OrderDate)
            .FirstOrDefaultAsync(o => o.Id == id);
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
    public async Task<Order?> GetOrderForCustomerAsync(int orderId, string phone)
    {
        var order = await GetOrderByIdAsync(orderId);
        if (order == null) return null;
        var normalizedPhone = NormalizePhone(phone);
        var orderPhone = NormalizePhone(order.CustomerPhone ?? "");
        return string.Equals(orderPhone, normalizedPhone, StringComparison.Ordinal) ? order : null;
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());
}
