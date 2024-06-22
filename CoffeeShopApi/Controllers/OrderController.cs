// Controllers/OrderController.cs
using Microsoft.AspNetCore.Mvc;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using System.Collections.Generic;
using System.Threading.Tasks;


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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        return Ok(await _orderService.GetOrdersAsync());
    }

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
            //await _notificationService.SendOrderNotificationAsync(createdOrder);
            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
        }

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
