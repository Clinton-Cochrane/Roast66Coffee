// Controllers/OrderController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;


namespace CoffeeShopApi.Controllers;
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly NotificationService _notificationService;

    public OrderController(OrderService orderService, NotificationService notificationService)
    {
        _orderService = orderService;
        _notificationService = notificationService;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        return Ok(await _orderService.GetOrdersAsync());
    }

    /// <summary>Public lookup: get order status by order ID and customer name (or phone for backward compatibility).</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<Order>> LookupOrder(
        [FromQuery] int orderId,
        [FromQuery] string? customerName,
        [FromQuery] string? phone,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(customerName)))
        {
            return BadRequest("Order ID and customer name are required.");
        }
        var order = await _orderService.GetOrderForCustomerAsync(orderId, phone, customerName, cancellationToken);
        if (order == null)
        {
            return NotFound("Order not found or customer details do not match.");
        }
        return Ok(order);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        return order;
    }

    [HttpPost]
    [EnableRateLimiting("Order")]
    public async Task<ActionResult<Order>> PostOrder(Order order, CancellationToken cancellationToken)
    {
        if (order.OrderItems == null || order.OrderItems.Count == 0)
        {
            ModelState.AddModelError("OrderItems", "The OrderItems field is required.");
            return BadRequest(ModelState);
        }

        var duplicate = await _orderService.FindDuplicateOrderAsync(order);
        if (duplicate != null)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                message = "Duplicate order detected. An identical order was placed recently.",
                existingOrderId = duplicate.Id,
                order = duplicate
            });
        }

        var createdOrder = await _orderService.CreateOrderAsync(order);
        await _notificationService.SendOrderNotificationAsync(createdOrder, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
    }

    [HttpGet("{orderId}/notifications")]
    public async Task<IActionResult> GetCustomerNotifications(
        int orderId,
        [FromQuery] string phone,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest("Order ID and phone are required.");
        }

        var order = await _orderService.GetOrderForCustomerAsync(orderId, phone, null, cancellationToken);
        if (order == null)
        {
            return NotFound("Order not found or phone does not match.");
        }

        var notifications = await _notificationService.GetCustomerNotificationsForOrderAsync(orderId, phone, cancellationToken);
        var result = notifications.Select(n => new
        {
            n.Id,
            n.EventType,
            n.TemplateKey,
            n.Status,
            n.CreatedUtc,
            n.SentUtc,
            n.UpdatedUtc
        });

        return Ok(result);
    }

    [HttpGet("{orderId}/summary")]
    public async Task<IActionResult> DownloadOrderSummary(
        int orderId,
        [FromQuery] string phone,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest("Order ID and phone are required.");
        }

        var order = await _orderService.GetOrderForCustomerAsync(orderId, phone, null, cancellationToken);
        if (order == null)
        {
            return NotFound("Order not found or phone does not match.");
        }

        var items = (order.OrderItems ?? [])
            .Select(item =>
            {
                var addOns = item.AddOns ?? [];
                var addOnTotal = addOns.Sum(a => (a.MenuItem?.Price ?? 0m) * a.Quantity);
                var itemTotal = (item.MenuItem?.Price ?? 0m) * item.Quantity;
                return new
                {
                    name = item.MenuItem?.Name ?? $"Item {item.MenuItemId}",
                    quantity = item.Quantity,
                    notes = item.Notes,
                    lineTotal = itemTotal + addOnTotal,
                    addOns = addOns.Select(a => new
                    {
                        name = a.MenuItem?.Name ?? $"Add-on {a.MenuItemId}",
                        quantity = a.Quantity,
                        lineTotal = (a.MenuItem?.Price ?? 0m) * a.Quantity
                    })
                };
            })
            .ToList();

        var total = items.Sum(x => (decimal)x.lineTotal);

        return Ok(new
        {
            orderId = order.Id,
            customerName = order.CustomerName,
            customerPhone = order.CustomerPhone,
            trackerUrl = "/order-status",
            status = order.OrderStatus.ToString(),
            items,
            total
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> PutOrder(int id, Order order)
    {
        if (id != order.Id)
        {
            return BadRequest();
        }

        var result = await _orderService.UpdateOrderAsync(order);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var result = await _orderService.DeleteOrderAsync(id);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
