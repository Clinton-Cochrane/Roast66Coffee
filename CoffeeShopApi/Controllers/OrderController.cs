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
    private readonly IStaffPushNotificationQueue _staffPushQueue;

    public OrderController(
        OrderService orderService,
        NotificationService notificationService,
        IStaffPushNotificationQueue staffPushQueue)
    {
        _orderService = orderService;
        _notificationService = notificationService;
        _staffPushQueue = staffPushQueue;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        return Ok(await _orderService.GetOrdersAsync());
    }

    [HttpGet("track")]
    [EnableRateLimiting("PublicTracking")]
    public IActionResult TrackOrderWithoutToken() => TrackingUnavailable();

    [HttpGet("track/{trackingToken}")]
    [EnableRateLimiting("PublicTracking")]
    public async Task<ActionResult<PublicOrderDto>> TrackOrder(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByTrackingTokenAsync(trackingToken, cancellationToken);
        if (order == null)
        {
            return TrackingUnavailable();
        }
        return Ok(PublicOrderDto.FromOrder(order));
    }

    [HttpGet("lookup")]
    [EnableRateLimiting("PublicTracking")]
    public IActionResult LegacyLookup() => NotFound();

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
                order = PublicOrderDto.FromOrder(duplicate)
            });
        }

        var createdOrder = await _orderService.CreateOrderAsync(order);
        _staffPushQueue.TryEnqueue(createdOrder.Id);
        return CreatedAtAction(
            nameof(TrackOrder),
            new { trackingToken = createdOrder.TrackingToken },
            PublicOrderDto.FromOrder(createdOrder));
    }

    [HttpGet("track/{trackingToken}/notifications")]
    [EnableRateLimiting("PublicTracking")]
    public async Task<IActionResult> GetCustomerNotifications(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByTrackingTokenAsync(trackingToken, cancellationToken);
        if (order == null)
        {
            return TrackingUnavailable();
        }

        var notifications = await _notificationService.GetCustomerNotificationsForOrderAsync(
            order.Id,
            order.CustomerPhone ?? string.Empty,
            cancellationToken);
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

    [HttpGet("track/{trackingToken}/summary")]
    [EnableRateLimiting("PublicTracking")]
    public async Task<IActionResult> DownloadOrderSummary(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByTrackingTokenAsync(trackingToken, cancellationToken);
        if (order == null)
        {
            return TrackingUnavailable();
        }

        var items = (order.OrderItems ?? [])
            .Select(item =>
            {
                var addOns = item.AddOns ?? [];
                var addOnTotal = addOns.Sum(a => a.UnitPrice * a.Quantity);
                var itemTotal = item.UnitPrice * item.Quantity;
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
                        lineTotal = a.UnitPrice * a.Quantity
                    })
                };
            })
            .ToList();

        var total = items.Sum(x => (decimal)x.lineTotal);

        return Ok(new
        {
            orderId = order.Id,
            customerName = order.CustomerName,
            trackerUrl = $"/order-status?token={order.TrackingToken}",
            status = order.OrderStatus.ToString(),
            items,
            total
        });
    }

    private NotFoundObjectResult TrackingUnavailable() =>
        NotFound(OrderTrackingUnavailableDto.Response);

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

}
