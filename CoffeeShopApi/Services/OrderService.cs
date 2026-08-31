using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    public async Task<OrderSubmissionResult> SubmitOrderAsync(
        Order order,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = idempotencyKey.Trim();
        var requestFingerprint = ComputeRequestFingerprint(order);
        var existing = await GetOrderByIdempotencyKeyAsync(normalizedKey, cancellationToken);
        if (existing != null)
        {
            EnsureMatchingRequest(existing, requestFingerprint);
            return new OrderSubmissionResult(existing, WasCreated: false);
        }

        order.IdempotencyKey = normalizedKey;
        order.RequestFingerprint = requestFingerprint;

        try
        {
            var created = await CreateOrderAsync(order, cancellationToken);
            return new OrderSubmissionResult(created, WasCreated: true);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
        {
            // SaveChanges is transactional, so the losing order graph was not persisted.
            // Clear it before loading the row committed by the concurrent winner.
            _context.ChangeTracker.Clear();
            existing = await GetOrderByIdempotencyKeyAsync(normalizedKey, cancellationToken);
            if (existing == null)
            {
                throw;
            }
            EnsureMatchingRequest(existing, requestFingerprint);
            return new OrderSubmissionResult(existing, WasCreated: false);
        }
    }

    private async Task<Order?> GetOrderByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns!)
            .ThenInclude(a => a.MenuItem)
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    private static void EnsureMatchingRequest(Order existing, string requestFingerprint)
    {
        if (!string.Equals(
                existing.RequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            throw new IdempotencyKeyConflictException(existing);
        }
    }

    private static bool IsIdempotencyKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_orders_idempotency_key"
        };

    internal static string ComputeRequestFingerprint(Order order)
    {
        var lines = (order.OrderItems ?? [])
            .Select(item =>
            {
                var addOns = (item.AddOns ?? [])
                    .Select(addOn => new FingerprintAddOn(addOn.MenuItemId, addOn.Quantity))
                    .OrderBy(addOn => addOn.MenuItemId)
                    .ThenBy(addOn => addOn.Quantity)
                    .ToList();
                return new FingerprintOrderLine(
                    item.MenuItemId,
                    item.Quantity,
                    NormalizeWhitespace(item.Notes),
                    addOns);
            })
            .OrderBy(line => line.MenuItemId)
            .ThenBy(line => line.Quantity)
            .ThenBy(line => line.Notes, StringComparer.Ordinal)
            .ThenBy(line => JsonSerializer.Serialize(line.AddOns), StringComparer.Ordinal)
            .ToList();

        var canonicalRequest = new FingerprintRequest(
            NormalizeName(order.CustomerName),
            NormalizePhone(order.CustomerPhone ?? string.Empty),
            NormalizeEmail(order.CustomerEmail),
            order.CustomerNotificationOptIn,
            lines);
        var json = JsonSerializer.Serialize(canonicalRequest);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private static string NormalizeWhitespace(string? value) =>
        string.Join(
            " ",
            (value ?? string.Empty).Trim().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeEmail(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
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
        if (order.OrderItems == null || order.OrderItems.Count == 0) return null;

        var incomingFingerprint = ComputeRequestFingerprint(order);

        var recentOrders = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.AddOns)
            .Where(o => o.OrderDate >= windowStart)
            .ToListAsync();

        var sameCustomer = recentOrders.Where(o => NormalizeCustomerKey(o) == customerKey);

        foreach (var existing in sameCustomer)
        {
            if (ComputeRequestFingerprint(existing) == incomingFingerprint)
                return existing;
        }
        return null;
    }

    private static string NormalizeCustomerKey(Order order)
    {
        var phone = NormalizePhone(order.CustomerPhone ?? "");
        if (!string.IsNullOrEmpty(phone)) return $"phone:{phone}";
        var name = NormalizeName(order.CustomerName);
        return string.IsNullOrEmpty(name) ? "" : $"name:{name}";
    }

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

    private sealed record FingerprintRequest(
        string CustomerName,
        string CustomerPhone,
        string? CustomerEmail,
        bool CustomerNotificationOptIn,
        IReadOnlyList<FingerprintOrderLine> OrderItems);

    private sealed record FingerprintOrderLine(
        int? MenuItemId,
        int Quantity,
        string Notes,
        IReadOnlyList<FingerprintAddOn> AddOns);

    private sealed record FingerprintAddOn(int? MenuItemId, int Quantity);
}

public sealed record OrderSubmissionResult(Order Order, bool WasCreated);

public sealed class IdempotencyKeyConflictException(Order existingOrder)
    : Exception("The idempotency key has already been used for a different order request.")
{
    public Order ExistingOrder { get; } = existingOrder;
}
