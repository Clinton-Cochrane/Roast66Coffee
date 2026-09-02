using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models;

public sealed class AdminOrderHistoryRequest : IValidatableObject
{
    public const int MaxPageNumber = int.MaxValue / 50;

    [Range(1, MaxPageNumber)]
    public int Page { get; set; } = 1;

    [StringLength(32)]
    public string Status { get; set; } = "all";

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    [StringLength(120)]
    public string? Search { get; set; }

    internal AdminOrderStatusFilter StatusFilter => (Status ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "all" => AdminOrderStatusFilter.All,
        "active" => AdminOrderStatusFilter.Active,
        "received" => AdminOrderStatusFilter.Received,
        "preparing" => AdminOrderStatusFilter.Preparing,
        "readyforpickup" => AdminOrderStatusFilter.ReadyForPickup,
        "completed" => AdminOrderStatusFilter.Completed,
        _ => AdminOrderStatusFilter.Invalid
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StatusFilter == AdminOrderStatusFilter.Invalid)
        {
            yield return new ValidationResult(
                "Status must be all, active, received, preparing, readyForPickup, or completed.",
                [nameof(Status)]);
        }

        if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value >= ToUtc.Value)
        {
            yield return new ValidationResult(
                "FromUtc must be earlier than ToUtc.",
                [nameof(FromUtc), nameof(ToUtc)]);
        }
    }
}

internal enum AdminOrderStatusFilter
{
    Invalid,
    All,
    Active,
    Received,
    Preparing,
    ReadyForPickup,
    Completed
}

public sealed class AdminOrderHistoryResponse
{
    public required IReadOnlyList<AdminOrderListItemDto> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalItems { get; init; }
    public required int TotalPages { get; init; }
    public required bool HasPreviousPage { get; init; }
    public required bool HasNextPage { get; init; }
}

public sealed class AdminOrderListItemDto
{
    public required int Id { get; init; }
    public required string CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public required DateTime OrderDate { get; init; }
    public required OrderStatus OrderStatus { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public DateTime? PaidUtc { get; init; }
    public string? PaymentProvider { get; init; }
    public string? LastStatusChangedBy { get; init; }
    public DateTime? LastStatusChangedUtc { get; init; }
    public required IReadOnlyList<AdminOrderLineItemDto> OrderItems { get; init; }
}

public sealed class AdminOrderLineItemDto
{
    public required int Id { get; init; }
    public required int Quantity { get; init; }
    public string? Notes { get; init; }
    public required string ItemName { get; init; }
    public required IReadOnlyList<AdminOrderAddOnDto> AddOns { get; init; }
}

public sealed class AdminOrderAddOnDto
{
    public required int Id { get; init; }
    public required int Quantity { get; init; }
    public required string ItemName { get; init; }
}
