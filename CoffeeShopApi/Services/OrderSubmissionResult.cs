using CoffeeShopApi.Models;

namespace CoffeeShopApi.Services;

public enum OrderSubmissionStatus
{
    Created,
    Duplicate,
    Invalid
}

public sealed record OrderSubmissionResult(
    OrderSubmissionStatus Status,
    Order? Order = null,
    Dictionary<string, string[]>? Errors = null);
