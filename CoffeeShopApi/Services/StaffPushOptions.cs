using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Services;

public sealed class StaffPushOptions
{
    public const string SectionName = "Push";

    [Range(1, 10_000)]
    public int QueueCapacity { get; set; } = 100;

    [Range(1, 100_000)]
    public int DeduplicationCapacity { get; set; } = 1_000;

    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromHours(12);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);

    [Range(1, 3)]
    public int MaxAttempts { get; set; } = 2;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
