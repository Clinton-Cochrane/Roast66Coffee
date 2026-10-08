using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models.Payments;

public sealed class ManualPaymentRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required]
    [RegularExpression("^(cash|other)$", ErrorMessage = "Method must be cash or other.")]
    public string Method { get; set; } = string.Empty;
}

public sealed record ManualPaymentResult(
    Guid PaymentId,
    int OrderId,
    string Method,
    decimal Amount,
    string Currency,
    DateTime PaidUtc,
    bool WasReplay);
