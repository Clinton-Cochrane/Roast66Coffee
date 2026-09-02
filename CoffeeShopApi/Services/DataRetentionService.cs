using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;
using CoffeeShopApi.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CoffeeShopApi.Services;

/// <summary>
/// Enforces the approved short-lived order and redacted operational-log lifecycles.
/// Each bounded batch commits independently so an interrupted run can safely resume.
/// PostgreSQL workers lock different rows with SKIP LOCKED when runs overlap.
/// </summary>
public sealed class DataRetentionService
{
    private readonly ApplicationDbContext _context;
    private readonly DataRetentionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly AuditEventFactory? _auditEvents;

    public DataRetentionService(
        ApplicationDbContext context,
        IOptions<DataRetentionOptions> options,
        TimeProvider timeProvider,
        AuditEventFactory? auditEvents = null)
    {
        _context = context;
        _options = options.Value;
        _timeProvider = timeProvider;
        _auditEvents = auditEvents;
    }

    public async Task<DataRetentionResult> PurgeExpiredDataAsync(
        CancellationToken cancellationToken = default,
        StaffActor? actor = null)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var completedOrderCutoffUtc = nowUtc.AddHours(-_options.CompletedOrderHours);
        var operationalLogCutoffUtc = nowUtc.AddDays(-_options.OperationalLogDays);

        var completedOrdersDeleted = 0;
        var paymentsDeleted = 0;
        var notificationLogsDeleted = 0;
        var auditEventsDeleted = 0;
        var batchesCompleted = 0;

        while (true)
        {
            var batch = await DeleteCompletedOrderBatchAsync(
                completedOrderCutoffUtc,
                cancellationToken);
            if (batch.Orders == 0)
            {
                break;
            }

            completedOrdersDeleted += batch.Orders;
            paymentsDeleted += batch.Payments;
            notificationLogsDeleted += batch.Notifications;
            batchesCompleted++;
        }

        while (true)
        {
            var deleted = await DeleteOrphanPaymentBatchAsync(
                completedOrderCutoffUtc,
                cancellationToken);
            if (deleted == 0)
            {
                break;
            }

            paymentsDeleted += deleted;
            batchesCompleted++;
        }

        while (true)
        {
            var deleted = await DeleteNotificationLogBatchAsync(
                operationalLogCutoffUtc,
                cancellationToken);
            if (deleted == 0)
            {
                break;
            }

            notificationLogsDeleted += deleted;
            batchesCompleted++;
        }

        while (true)
        {
            var deleted = await DeleteAuditEventBatchAsync(
                operationalLogCutoffUtc,
                cancellationToken);
            if (deleted == 0)
            {
                break;
            }

            auditEventsDeleted += deleted;
            batchesCompleted++;
        }

        var result = new DataRetentionResult(
            completedOrdersDeleted,
            paymentsDeleted,
            notificationLogsDeleted,
            auditEventsDeleted,
            batchesCompleted);

        if (actor != null && _auditEvents != null)
        {
            _auditEvents.Add(
                actor,
                "data_retention.purged",
                "data_retention",
                "all",
                new
                {
                    result.CompletedOrdersDeleted,
                    result.PaymentsDeleted,
                    result.NotificationLogsDeleted,
                    result.AuditEventsDeleted,
                    CompletedOrderCutoffUtc = completedOrderCutoffUtc,
                    OperationalLogCutoffUtc = operationalLogCutoffUtc
                });
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async Task<(int Orders, int Payments, int Notifications)> DeleteCompletedOrderBatchAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var orders = await SelectCompletedOrdersAsync(cutoffUtc, cancellationToken);
        if (orders.Count == 0)
        {
            await CommitAsync(transaction, cancellationToken);
            return (0, 0, 0);
        }

        var orderIds = orders.Select(order => order.Id).ToList();
        var payments = await _context.Payments
            .Where(payment => payment.OrderId != null && orderIds.Contains(payment.OrderId.Value))
            .ToListAsync(cancellationToken);
        var notifications = await _context.NotificationMessages
            .Where(notification => notification.OrderId != null && orderIds.Contains(notification.OrderId.Value))
            .ToListAsync(cancellationToken);

        _context.NotificationMessages.RemoveRange(notifications);
        _context.Payments.RemoveRange(payments);
        _context.Orders.RemoveRange(orders);
        await _context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        _context.ChangeTracker.Clear();
        return (orders.Count, payments.Count, notifications.Count);
    }

    private async Task<int> DeleteOrphanPaymentBatchAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var payments = await SelectOrphanPaymentsAsync(cutoffUtc, cancellationToken);
        if (payments.Count == 0)
        {
            await CommitAsync(transaction, cancellationToken);
            return 0;
        }

        _context.Payments.RemoveRange(payments);
        await _context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        _context.ChangeTracker.Clear();
        return payments.Count;
    }

    private async Task<int> DeleteNotificationLogBatchAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var notifications = await SelectNotificationLogsAsync(cutoffUtc, cancellationToken);
        if (notifications.Count == 0)
        {
            await CommitAsync(transaction, cancellationToken);
            return 0;
        }

        _context.NotificationMessages.RemoveRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        _context.ChangeTracker.Clear();
        return notifications.Count;
    }

    private async Task<int> DeleteAuditEventBatchAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var auditEvents = await SelectAuditEventsAsync(cutoffUtc, cancellationToken);
        if (auditEvents.Count == 0)
        {
            await CommitAsync(transaction, cancellationToken);
            return 0;
        }

        var ids = auditEvents.Select(auditEvent => auditEvent.Id).ToList();
        int deleted;
        if (_context.Database.IsRelational())
        {
            deleted = await _context.AuditEvents
                .Where(auditEvent => ids.Contains(auditEvent.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            deleted = await _context.DeleteExpiredAuditEventsAsync(ids, cancellationToken);
        }

        await CommitAsync(transaction, cancellationToken);
        _context.ChangeTracker.Clear();
        return deleted;
    }

    private Task<List<Order>> SelectCompletedOrdersAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            return _context.Orders
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM orders
                    WHERE orderstatus = {(int)OrderStatus.Completed}
                      AND completedutc <= {cutoffUtc}
                    ORDER BY completedutc, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT {_options.BatchSize}
                    """)
                .ToListAsync(cancellationToken);
        }

        return _context.Orders
            .Where(order =>
                order.OrderStatus == OrderStatus.Completed &&
                order.CompletedUtc != null &&
                order.CompletedUtc <= cutoffUtc)
            .OrderBy(order => order.CompletedUtc)
            .ThenBy(order => order.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private Task<List<Payment>> SelectOrphanPaymentsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            return _context.Payments
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM payments
                    WHERE orderid IS NULL
                      AND createdutc <= {cutoffUtc}
                    ORDER BY createdutc, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT {_options.BatchSize}
                    """)
                .ToListAsync(cancellationToken);
        }

        return _context.Payments
            .Where(payment => payment.OrderId == null && payment.CreatedUtc <= cutoffUtc)
            .OrderBy(payment => payment.CreatedUtc)
            .ThenBy(payment => payment.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private Task<List<NotificationMessage>> SelectNotificationLogsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            return _context.NotificationMessages
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM notificationmessages
                    WHERE createdutc <= {cutoffUtc}
                    ORDER BY createdutc, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT {_options.BatchSize}
                    """)
                .ToListAsync(cancellationToken);
        }

        return _context.NotificationMessages
            .Where(notification => notification.CreatedUtc <= cutoffUtc)
            .OrderBy(notification => notification.CreatedUtc)
            .ThenBy(notification => notification.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private Task<List<AuditEvent>> SelectAuditEventsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            return _context.AuditEvents
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM auditevents
                    WHERE occurredutc <= {cutoffUtc}
                    ORDER BY occurredutc, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT {_options.BatchSize}
                    """)
                .ToListAsync(cancellationToken);
        }

        return _context.AuditEvents
            .Where(auditEvent => auditEvent.OccurredUtc <= cutoffUtc)
            .OrderBy(auditEvent => auditEvent.OccurredUtc)
            .ThenBy(auditEvent => auditEvent.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private static Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;
}
