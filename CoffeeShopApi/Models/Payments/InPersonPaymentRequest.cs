using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models.Payments;

public sealed class InPersonPaymentRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }
}

public sealed record InPersonPaymentResult(
    Guid PaymentId,
    int OrderId,
    string Provider,
    string Status,
    decimal Amount,
    string Currency,
    DateTime? PaidUtc);
