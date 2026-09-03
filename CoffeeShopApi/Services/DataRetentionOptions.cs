using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Services;

public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    [Range(1, 168)]
    public int CompletedOrderHours { get; set; } = 48;

    [Range(1, 365)]
    public int OperationalLogDays { get; set; } = 90;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    [Range(1, 1440)]
    public int CheckIntervalMinutes { get; set; } = 60;

    [Range(1, 1440)]
    public int RetryDelayMinutes { get; set; } = 5;
}
