// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using CoffeeShopApi.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly OrderService _orderService;
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly NotificationSettingsService _notificationSettingsService;


        public AdminController(ApplicationDbContext context, OrderService orderService, NotificationService notificationService, IConfiguration configuration, NotificationSettingsService notificationSettingsService)
        {
            _context = context;
            _orderService = orderService;
            _notificationService = notificationService;
            _configuration = configuration;
            _notificationSettingsService = notificationSettingsService;
        }

        // Get all menu items
        [HttpGet("menu")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetMenuItems()
        {
            return await _context.MenuItems.ToListAsync();
        }

        // Add a new menu item
        [HttpPost("menu")]
        public async Task<ActionResult<MenuItem>> PostMenuItem(MenuItem menuItem)
        {
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMenuItems), new { id = menuItem.Id }, menuItem);
        }

        // Update an existing menu item
        [HttpPut("menu/{id}")]
        public async Task<IActionResult> PutMenuItem(int id, MenuItem menuItem)
        {
            if (id != menuItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(menuItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MenuItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // Delete a menu item
        [HttpDelete("menu/{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }

            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }

        // Get all orders
        [HttpGet("orders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            var orders = await _orderService.GetOrdersAsync();
            return Ok(orders);
        }

        [HttpPost("orders")]
        public async Task<ActionResult<Order>> PostOrder(Order order)
        {
            var newOrder = await _orderService.CreateOrderAsync(order);
            await _notificationService.SendOrderNotificationAsync(newOrder);
            return CreatedAtAction(nameof(GetOrders), new { id = newOrder.Id }, newOrder);
        }

        [HttpGet("notificationSettings")]
        public async Task<ActionResult<NotificationSettings>> GetNotificationSettings()
        {
            var settings = await _notificationSettingsService.GetNotificationSettingsAsync();
            if (settings == null)
            {
                return NotFound();
            }
            return Ok(settings);
        }

        [HttpPost("notificationSettings")]
        public async Task<IActionResult> SaveNotificationSettings([FromBody] NotificationSettingsModel model)
        {
            var settings = new NotificationSettings
            {
                PhoneNumber = model.PhoneNumber
            };
            await _notificationSettingsService.SaveNotificationSettingsAsync(settings);
            return Ok();
        }

        [HttpGet("/ping")]
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            Console.WriteLine("Ping endpoint was called");
            return Ok("pong");
        }

        [HttpPost("seed-menu")]
        public async Task<IActionResult> SeedMenuItems()
        {
            try
            {
                await Data.SeedMenuItems.SeedAsync(_context);
                return Ok("Menu items have been seeded successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding menu items: {ex.Message}");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }



    public class NotificationSettingsModel
    {
        public required string PhoneNumber { get; set; }
    }
}
