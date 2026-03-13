// Controllers/OrderController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

    /// <summary>Public lookup: get order status by order ID and phone. For customer tracking.</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<Order>> LookupOrder([FromQuery] int orderId, [FromQuery] string phone)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest("Order ID and phone are required.");
        }
        var order = await _orderService.GetOrderForCustomerAsync(orderId, phone);
        if (order == null)
        {
            return NotFound("Order not found or phone does not match.");
        }
        return Ok(order);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        return order;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> PostOrder(Order order)
    {
        if (order.OrderItems == null || order.OrderItems.Count == 0)
        {
            ModelState.AddModelError("OrderItems", "The OrderItems field is required.");
            return BadRequest(ModelState);
        }

        var createdOrder = await _orderService.CreateOrderAsync(order);
        await _notificationService.SendOrderNotificationAsync(createdOrder);
        return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
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
