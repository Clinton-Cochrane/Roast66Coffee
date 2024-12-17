// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Data;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;

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

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel login)
        {
            if (login.Username == "admin" && login.Password == "password") // Replace with proper validation
            {
                var token = GenerateToken();
                return Ok(new { token });
            }

            return Unauthorized();
        }

        private string GenerateToken()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "default_key"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
        new Claim(ClaimTypes.Name, "admin"),
        new Claim(ClaimTypes.Role, "Admin")
    };
            var tokenExpiry = int.TryParse(_configuration["Jwt:TokenExpiryInHours"], out var parseId) ? parseId : 0;

            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddHours(tokenExpiry),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public class LoginModel
        {
            public required string Username { get; set; }
            public required string Password { get; set; }

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

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            Console.WriteLine("Ping endpoint was called");
            return Ok("pong");
        }

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            var categories = Enum.GetValues(typeof(CategoryType))
                .Cast<CategoryType>()
                .Select(ct => new { id = (int)ct, name = FormatCategoryName(ct.ToString()) });
            return Ok(categories);
        }

        private string FormatCategoryName(string name)
        {
            // Insert spaces before capital letters for better readability
            return System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
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
