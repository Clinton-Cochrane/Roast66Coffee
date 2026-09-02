using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Security;

namespace CoffeeShopApi.Services;

public class NotificationRetentionService
{
    public const int RetentionDays = 30;

    private readonly ApplicationDbContext _context;
    private readonly AuditEventFactory? _auditEvents;

    public NotificationRetentionService(ApplicationDbContext context, AuditEventFactory? auditEvents = null)
    {
        _context = context;
        _auditEvents = auditEvents;
    }

    public async Task<int> PurgeNotificationsOlderThanAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default,
        StaffActor? actor = null)
    {
        var stale = await _context.NotificationMessages
            .Where(x => x.CreatedUtc < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (actor != null && _auditEvents != null)
        {
            _auditEvents.Add(
                actor,
                "notification_logs.purged",
                "notification_log",
                "all",
                new { DeletedCount = stale.Count, CutoffUtc = cutoffUtc });
        }

        if (stale.Count > 0) _context.NotificationMessages.RemoveRange(stale);
        if (stale.Count == 0 && actor == null) return 0;
        await _context.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
