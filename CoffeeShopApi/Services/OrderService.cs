using CoffeeShopApi.Models;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoffeeShopApi.Services;

/// <summary>
/// Owns order persistence invariants: immutable menu snapshots, durable submission
/// idempotency, private tracking tokens, bounded admin history, and concurrency-safe
/// status transitions. Controllers should delegate these rules instead of updating
/// order graphs directly.
/// </summary>
public class OrderService(ApplicationDbContext context, IConfiguration configuration)
{
    /// <summary>
    /// The operational order history deliberately uses a fixed page size. Revisit this
    /// contract if normal order volume grows beyond roughly 50 orders per day.
    /// </summary>
    public const int AdminOrderHistoryPageSize = 50;

    /// <summary>
    /// Completed orders remain visible to staff for a bounded operational window;
    /// this does not delete the underlying business record.
    /// </summary>
    public const int CompletedOrderRetentionHours = 30;

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

    /// <summary>
    /// Builds the staff queue with active orders first, then recent completed orders.
    /// <paramref name="request"/> date bounds are UTC and the upper bound is exclusive,
    /// allowing callers to express a whole local day as [start, next day).
    /// </summary>
    public async Task<AdminOrderHistoryResponse> GetOrderHistoryAsync(
        AdminOrderHistoryRequest request,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var retentionCutoff = AsUtc(nowUtc).AddHours(-CompletedOrderRetentionHours);
        var query = _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.OrderStatus != OrderStatus.Completed ||
                (order.CompletedUtc.HasValue && order.CompletedUtc.Value >= retentionCutoff));

        query = request.StatusFilter switch
        {
            AdminOrderStatusFilter.Active => query.Where(order => order.OrderStatus != OrderStatus.Completed),
            AdminOrderStatusFilter.Received => query.Where(order => order.OrderStatus == OrderStatus.Received),
            AdminOrderStatusFilter.Preparing => query.Where(order => order.OrderStatus == OrderStatus.Preparing),
            AdminOrderStatusFilter.ReadyForPickup => query.Where(order => order.OrderStatus == OrderStatus.ReadyForPickup),
            AdminOrderStatusFilter.Completed => query.Where(order => order.OrderStatus == OrderStatus.Completed),
            _ => query
        };

        if (request.FromUtc.HasValue)
        {
            var fromUtc = AsUtc(request.FromUtc.Value);
            query = query.Where(order => order.OrderDate >= fromUtc);
        }

        if (request.ToUtc.HasValue)
        {
            var toUtc = AsUtc(request.ToUtc.Value);
            query = query.Where(order => order.OrderDate < toUtc);
        }

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(search))
        {
            var matchesOrderId = int.TryParse(search, out var orderId);
            var phoneSearch = new string(search.Where(char.IsDigit).ToArray());
            query = query.Where(order =>
                (matchesOrderId && order.Id == orderId) ||
                order.CustomerName.ToLower().Contains(search) ||
                (order.CustomerPhone != null &&
                    ((!string.IsNullOrEmpty(phoneSearch) &&
                        order.CustomerPhone
                            .Replace("+", "")
                            .Replace("(", "")
                            .Replace(")", "")
                            .Replace("-", "")
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Contains(phoneSearch)) ||
                     order.CustomerPhone.ToLower().Contains(search))) ||
                order.OrderItems.Any(item =>
                    item.ItemName.ToLower().Contains(search) ||
                    item.AddOns!.Any(addOn => addOn.ItemName.ToLower().Contains(search))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)AdminOrderHistoryPageSize);
        var items = await query
            .OrderBy(order => order.OrderStatus == OrderStatus.Completed)
            .ThenByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .Skip((request.Page - 1) * AdminOrderHistoryPageSize)
            .Take(AdminOrderHistoryPageSize)
            .Select(order => new AdminOrderListItemDto
            {
                Id = order.Id,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                OrderDate = order.OrderDate,
                OrderStatus = order.OrderStatus,
                CompletedUtc = order.CompletedUtc,
                PaidUtc = order.PaidUtc,
                PaymentProvider = order.PaymentProvider,
                OrderItems = order.OrderItems
                    .Select(item => new AdminOrderLineItemDto
                    {
                        Id = item.Id,
                        Quantity = item.Quantity,
                        Notes = item.Notes,
                        ItemName = item.ItemName,
                        AddOns = item.AddOns!
                            .Select(addOn => new AdminOrderAddOnDto
                            {
                                Id = addOn.Id,
                                Quantity = addOn.Quantity,
                                ItemName = addOn.ItemName
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new AdminOrderHistoryResponse
        {
            Items = items,
            Page = request.Page,
            PageSize = AdminOrderHistoryPageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasPreviousPage = request.Page > 1,
            HasNextPage = request.Page < totalPages
        };
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
    /// Creates an order once for a durable client key. Equivalent retries replay the
    /// stored order; reusing the key for different normalized content is a conflict.
    /// The unique database constraint selects the winner when requests race.
    /// </summary>
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

    /// <summary>
    /// Produces a stable hash of customer intent, ignoring harmless whitespace,
    /// casing, and line/add-on ordering while retaining quantities and selections.
    /// This fingerprint is a replay comparison, not an authentication primitive.
    /// </summary>
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
    /// Finds a recent same-customer order with equivalent normalized content.
    /// This legacy/operator heuristic is time-bounded and is separate from the durable
    /// idempotency-key contract used by public submission.
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

    /// <summary>
    /// Validates every requested menu reference and stamps names, descriptions,
    /// categories, and effective prices onto the order graph before saving. Historical
    /// receipts therefore remain stable when the live menu later changes or is archived.
    /// </summary>
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

    /// <summary>
    /// Generates 256 random bits encoded as an unpadded, URL-safe 43-character token.
    /// The token is the only credential for the public tracking route.
    /// </summary>
    internal static string GenerateTrackingToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// Updates editable order data while preserving the persisted status fields.
    /// Status changes must go through <see cref="AdvanceStatusAsync(int, OrderStatus, CancellationToken)"/>
    /// so clients cannot bypass the state machine or its concurrency token.
    /// </summary>
    public async Task<bool> UpdateOrderAsync(Order order)
    {
        var persistedStatus = await _context.Orders
            .AsNoTracking()
            .Where(existing => existing.Id == order.Id)
            .Select(existing => new
            {
                existing.OrderStatus,
                existing.CompletedUtc,
                existing.StatusConcurrencyToken
            })
            .SingleOrDefaultAsync();
        if (persistedStatus == null)
        {
            return false;
        }

        order.OrderStatus = persistedStatus.OrderStatus;
        order.CompletedUtc = persistedStatus.CompletedUtc;
        order.StatusConcurrencyToken = persistedStatus.StatusConcurrencyToken;
        _context.Entry(order).State = EntityState.Modified;
        _context.Entry(order).Property(existing => existing.StatusConcurrencyToken).IsModified = false;
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

    /// <summary>
    /// Advances exactly one state when the caller's expected status is current.
    /// A repeated successful request is reported as a replay; competing or stale
    /// transitions return a conflict after reloading the database winner.
    /// </summary>
    internal Task<OrderStatusAdvanceResult> AdvanceStatusAsync(
        int orderId,
        OrderStatus expectedStatus,
        CancellationToken cancellationToken = default) =>
        AdvanceStatusAsync(orderId, expectedStatus, DateTime.UtcNow, cancellationToken);

    internal async Task<OrderStatusAdvanceResult> AdvanceStatusAsync(
        int orderId,
        OrderStatus expectedStatus,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(expectedStatus))
        {
            return OrderStatusAdvanceResult.InvalidExpected(expectedStatus);
        }

        var order = await _context.Orders
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        if (order == null)
        {
            return OrderStatusAdvanceResult.NotFound(orderId);
        }

        if (!Enum.IsDefined(order.OrderStatus))
        {
            return OrderStatusAdvanceResult.InvalidCurrent(order, expectedStatus);
        }

        if (order.OrderStatus == OrderStatus.Completed && expectedStatus == OrderStatus.Completed)
        {
            return OrderStatusAdvanceResult.Terminal(order);
        }

        if (order.OrderStatus != expectedStatus)
        {
            if (OrderStatusStateMachine.TryGetNext(expectedStatus, out var replayedStatus) &&
                replayedStatus == order.OrderStatus)
            {
                return OrderStatusAdvanceResult.Replayed(order, expectedStatus);
            }

            return OrderStatusAdvanceResult.Conflict(order, expectedStatus);
        }

        if (!OrderStatusStateMachine.TryGetNext(order.OrderStatus, out var nextStatus))
        {
            return OrderStatusAdvanceResult.Terminal(order);
        }

        var previousStatus = order.OrderStatus;
        order.OrderStatus = nextStatus;
        if (nextStatus == OrderStatus.Completed)
        {
            order.CompletedUtc = AsUtc(nowUtc);
        }
        order.StatusConcurrencyToken = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return OrderStatusAdvanceResult.Advanced(order, previousStatus);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            var current = await _context.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
            if (current == null)
            {
                return OrderStatusAdvanceResult.NotFound(orderId);
            }
            if (current.OrderStatus == nextStatus)
            {
                return OrderStatusAdvanceResult.Replayed(current, expectedStatus);
            }
            return OrderStatusAdvanceResult.Conflict(current, expectedStatus);
        }
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

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

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
