using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CoffeeShopApi.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOrderRequest : IValidatableObject
{
    public const int MaxCustomerNameLength = 100;
    public const int MaxPrimaryLines = 20;
    public const int MaxDrinkUnits = 50;

    [Required(ErrorMessage = "Customer name is required.")]
    public string? CustomerName { get; init; }

    [Required(ErrorMessage = "At least one order item is required.")]
    [MinLength(1, ErrorMessage = "At least one order item is required.")]
    [MaxLength(MaxPrimaryLines, ErrorMessage = "An order cannot contain more than 20 primary lines.")]
    public List<CreateOrderItemRequest>? OrderItems { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var normalizedName = CustomerName?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            yield return new ValidationResult(
                "Customer name cannot be empty or whitespace.",
                [nameof(CustomerName)]);
        }
        else if (normalizedName.Length > MaxCustomerNameLength)
        {
            yield return new ValidationResult(
                $"Customer name cannot exceed {MaxCustomerNameLength} characters.",
                [nameof(CustomerName)]);
        }

        var nonNullItems = (OrderItems ?? []).OfType<CreateOrderItemRequest>().ToList();
        if (nonNullItems.Count != (OrderItems?.Count ?? 0))
        {
            yield return new ValidationResult(
                "Order items cannot contain null entries.",
                [nameof(OrderItems)]);
        }

        var totalUnits = nonNullItems.Sum(item => (long)item.Quantity);
        if (totalUnits > MaxDrinkUnits)
        {
            yield return new ValidationResult(
                $"An order cannot contain more than {MaxDrinkUnits} drink units.",
                [nameof(OrderItems)]);
        }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOrderItemRequest : IValidatableObject
{
    public const int MaxQuantity = 12;
    public const int MaxNotesLength = 500;
    public const int MaxDistinctFlavors = 12;

    [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must identify a menu item.")]
    public int MenuItemId { get; init; }

    [Range(1, MaxQuantity, ErrorMessage = "Quantity must be between 1 and 12.")]
    public int Quantity { get; init; }

    public string? Notes { get; init; }

    [MaxLength(MaxDistinctFlavors, ErrorMessage = "A drink cannot contain more than 12 distinct flavors.")]
    public List<CreateOrderAddOnRequest>? AddOns { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Notes?.Trim().Length ?? 0) > MaxNotesLength)
        {
            yield return new ValidationResult(
                $"Notes cannot exceed {MaxNotesLength} characters.",
                [nameof(Notes)]);
        }

        var nonNullAddOns = (AddOns ?? []).OfType<CreateOrderAddOnRequest>().ToList();
        if (nonNullAddOns.Count != (AddOns?.Count ?? 0))
        {
            yield return new ValidationResult(
                "Add-ons cannot contain null entries.",
                [nameof(AddOns)]);
        }

        var duplicateFlavorIds = nonNullAddOns
            .GroupBy(addOn => addOn.MenuItemId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateFlavorIds.Count > 0)
        {
            yield return new ValidationResult(
                "Each flavor may appear only once per drink.",
                [nameof(AddOns)]);
        }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOrderAddOnRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must identify a menu item.")]
    public int MenuItemId { get; init; }

    [Range(1, CreateOrderItemRequest.MaxQuantity, ErrorMessage = "Quantity must be between 1 and 12.")]
    public int Quantity { get; init; }
}
