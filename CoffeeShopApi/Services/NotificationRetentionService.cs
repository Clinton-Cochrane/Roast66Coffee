using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services;

public class NotificationRetentionService
{
    private readonly ApplicationDbContext _context;

    public NotificationRetentionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> PurgeEmailNotificationsOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var stale = await _context.NotificationMessages
            .Where(x => x.Channel == "email" && x.CreatedUtc < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        _context.NotificationMessages.RemoveRange(stale);
        await _context.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
