using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models;

public sealed class AdvanceOrderStatusRequest
{
    [Required]
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus? ExpectedStatus { get; init; }
}

internal static class OrderStatusStateMachine
{
    internal static bool TryGetNext(OrderStatus current, out OrderStatus next)
    {
        switch (current)
        {
            case OrderStatus.Received:
                next = OrderStatus.Preparing;
                return true;
            case OrderStatus.Preparing:
                next = OrderStatus.ReadyForPickup;
                return true;
            case OrderStatus.ReadyForPickup:
                next = OrderStatus.Completed;
                return true;
            default:
                next = current;
                return false;
        }
    }
}
