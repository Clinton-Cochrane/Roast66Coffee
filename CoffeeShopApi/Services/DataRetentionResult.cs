namespace CoffeeShopApi.Services;

public sealed record DataRetentionResult(
    int CompletedOrdersDeleted,
    int PaymentsDeleted,
    int NotificationLogsDeleted,
    int AuditEventsDeleted,
    int BatchesCompleted)
{
    public int TotalDeleted =>
        CompletedOrdersDeleted + PaymentsDeleted + NotificationLogsDeleted + AuditEventsDeleted;
}
