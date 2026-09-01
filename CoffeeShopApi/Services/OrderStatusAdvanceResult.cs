using CoffeeShopApi.Models;

namespace CoffeeShopApi.Services;

internal enum OrderStatusAdvanceOutcome
{
    Advanced,
    Replayed,
    Terminal,
    NotFound,
    Conflict,
    InvalidExpectedStatus,
    InvalidCurrentStatus
}

internal sealed record OrderStatusAdvanceResult(
    OrderStatusAdvanceOutcome Outcome,
    int OrderId,
    OrderStatus ExpectedStatus,
    Order? Order,
    string Message)
{
    internal bool Changed => Outcome == OrderStatusAdvanceOutcome.Advanced;

    internal static OrderStatusAdvanceResult Advanced(Order order, OrderStatus previousStatus) =>
        new(
            OrderStatusAdvanceOutcome.Advanced,
            order.Id,
            previousStatus,
            order,
            "Order status advanced successfully.");

    internal static OrderStatusAdvanceResult Replayed(Order order, OrderStatus expectedStatus) =>
        new(
            OrderStatusAdvanceOutcome.Replayed,
            order.Id,
            expectedStatus,
            order,
            "This status advance was already applied.");

    internal static OrderStatusAdvanceResult Terminal(Order order) =>
        new(
            OrderStatusAdvanceOutcome.Terminal,
            order.Id,
            OrderStatus.Completed,
            order,
            "Completed orders are terminal; no status change was made.");

    internal static OrderStatusAdvanceResult NotFound(int orderId) =>
        new(
            OrderStatusAdvanceOutcome.NotFound,
            orderId,
            default,
            null,
            "Order not found.");

    internal static OrderStatusAdvanceResult Conflict(Order order, OrderStatus expectedStatus) =>
        new(
            OrderStatusAdvanceOutcome.Conflict,
            order.Id,
            expectedStatus,
            order,
            $"Order status is {order.OrderStatus}, not {expectedStatus}. Refresh orders before advancing again.");

    internal static OrderStatusAdvanceResult InvalidExpected(OrderStatus expectedStatus) =>
        new(
            OrderStatusAdvanceOutcome.InvalidExpectedStatus,
            0,
            expectedStatus,
            null,
            $"Expected status value {(int)expectedStatus} is not defined.");

    internal static OrderStatusAdvanceResult InvalidCurrent(
        Order order,
        OrderStatus expectedStatus) =>
        new(
            OrderStatusAdvanceOutcome.InvalidCurrentStatus,
            order.Id,
            expectedStatus,
            order,
            $"Order {order.Id} has undefined status value {(int)order.OrderStatus} and cannot be advanced.");
}
