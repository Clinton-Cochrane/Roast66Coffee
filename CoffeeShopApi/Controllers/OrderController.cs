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
    internal const int MaxIdempotencyKeyLength = 128;
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

    [AllowAnonymous]
    [HttpGet("track")]
    [EnableRateLimiting("PublicTracking")]
    public IActionResult TrackOrderWithoutToken() => TrackingUnavailable();

    [AllowAnonymous]
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

    [AllowAnonymous]
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

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("Order")]
    public async Task<ActionResult<PublicOrderDto>> PostOrder(
        Order order,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (order.OrderItems == null || order.OrderItems.Count == 0)
        {
            ModelState.AddModelError("OrderItems", "The OrderItems field is required.");
            return BadRequest(ModelState);
        }

        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > MaxIdempotencyKeyLength)
        {
            return BadRequest(new
            {
                message = $"X-Idempotency-Key is required and must be at most {MaxIdempotencyKeyLength} characters."
            });
        }

        OrderSubmissionResult submission;
        try
        {
            submission = await _orderService.SubmitOrderAsync(order, key, cancellationToken);
        }
        catch (UnavailableMenuItemsException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (IdempotencyKeyConflictException ex)
        {
            return Conflict(new
            {
                message = ex.Message,
                existingOrderId = ex.ExistingOrder.Id
            });
        }

        if (!submission.WasCreated)
        {
            Response.Headers.Append("Idempotency-Replayed", "true");
            return Ok(PublicOrderDto.FromOrder(submission.Order));
        }

        _staffPushQueue.TryEnqueue(submission.Order.Id);
        return CreatedAtAction(
            nameof(TrackOrder),
            new { trackingToken = submission.Order.TrackingToken },
            PublicOrderDto.FromOrder(submission.Order));
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
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
                    name = item.ItemName,
                    quantity = item.Quantity,
                    notes = item.Notes,
                    lineTotal = itemTotal + addOnTotal,
                    addOns = addOns.Select(a => new
                    {
                        name = a.ItemName,
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
