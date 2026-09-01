using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Security;

namespace CoffeeShopApi.Services;

public sealed class AuditEventFactory(ApplicationDbContext context)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _context = context;

    public AuditEvent Add(
        StaffActor actor,
        string action,
        string entityType,
        string entityId,
        object? details = null,
        DateTime? occurredUtc = null)
    {
        var auditEvent = new AuditEvent
        {
            OccurredUtc = occurredUtc ?? DateTime.UtcNow,
            ActorUserId = actor.UserId,
            ActorDisplayName = actor.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = details == null ? "{}" : JsonSerializer.Serialize(details, JsonOptions)
        };
        _context.AuditEvents.Add(auditEvent);
        return auditEvent;
    }
}
