// Controllers/AdminController.cs
using CoffeeShopApi;
using System.ComponentModel.DataAnnotations;
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
using Microsoft.AspNetCore.RateLimiting;
using System.Linq;

namespace CoffeeShopApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly OrderService _orderService;
        private readonly MenuService _menuService;
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly NotificationSettingsService _notificationSettingsService;
        private readonly SupportEmailService _supportEmailService;


        public AdminController(
            ApplicationDbContext context,
            OrderService orderService,
            MenuService menuService,
            NotificationService notificationService,
            IConfiguration configuration,
            NotificationSettingsService notificationSettingsService,
            SupportEmailService supportEmailService)
        {
            _context = context;
            _orderService = orderService;
            _menuService = menuService;
            _notificationService = notificationService;
            _configuration = configuration;
            _notificationSettingsService = notificationSettingsService;
            _supportEmailService = supportEmailService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("Login")]
        public IActionResult Login([FromBody] LoginModel login)
        {
            var adminUser = _configuration["Admin:Username"] ?? "admin";
            var adminPassword = _configuration["Admin:Password"] ?? "password";
            if (string.Equals(login.Username, adminUser, StringComparison.Ordinal) &&
                string.Equals(login.Password, adminPassword, StringComparison.Ordinal))
            {
                var token = GenerateToken();
                return Ok(new { token });
            }

            return Unauthorized();
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest? request,
            CancellationToken cancellationToken)
        {
            if (!_supportEmailService.IsConfigured())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Password support is not configured right now."
                });
            }

            var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _supportEmailService.SendForgotPasswordAlertAsync(sourceIp, request?.Message, cancellationToken);

            return Ok(new
            {
                message = "If support is available, your request has been sent."
            });
        }


        private string GenerateToken()
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            {
                throw new InvalidOperationException("JWT key is invalid or too short. It must be at least 32 characters long.");
            }
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? "default_key"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var tokenExpiry = int.TryParse(_configuration["Jwt:TokenExpiryInHours"], out var parsedHours) && parsedHours > 0
                ? parsedHours
                : 1;

            var token = new JwtSecurityToken(
                JwtTokenSettings.GetIssuer(_configuration),
                JwtTokenSettings.GetAudience(_configuration),
                claims,
                expires: DateTime.Now.AddHours(tokenExpiry),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public class LoginModel
        {
            [Required(ErrorMessage = "Username is required")]
            [StringLength(50, MinimumLength = 1)]
            public required string Username { get; set; }

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, MinimumLength = 1)]
            public required string Password { get; set; }
        }


        // Get all menu items
        [HttpGet("menu")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetMenuItems()
        {
            return await _context.MenuItems.ToListAsync();
        }

        // Add a new menu item
        [Authorize(Roles = "Admin")]
        [HttpPost("menu")]
        public async Task<ActionResult<MenuItem>> PostMenuItem(MenuItem menuItem)
        {
            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMenuItems), new { id = menuItem.Id }, menuItem);
        }

        // Update an existing menu item
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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

        [Authorize(Roles = "Admin")]
        // Get all orders
        [HttpGet("orders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            var orders = await _orderService.GetOrdersAsync();
            return Ok(orders);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("orders/new-count")]
        public async Task<ActionResult<object>> GetNewOrdersCount([FromQuery] DateTime since)
        {
            var count = await _orderService.GetCountSinceAsync(since.ToUniversalTime());
            return Ok(new { count });
        }

        [HttpPost("orders")]
        [EnableRateLimiting("Order")]
        public async Task<ActionResult<Order>> PostOrder(Order order, CancellationToken cancellationToken)
        {
            if (order.OrderItems == null || order.OrderItems.Count == 0)
            {
                return BadRequest(new { message = "At least one order item is required." });
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
            var newOrder = await _orderService.CreateOrderAsync(order);
            await _notificationService.SendOrderNotificationAsync(newOrder, cancellationToken);
            return CreatedAtAction(nameof(GetOrders), new { id = newOrder.Id }, newOrder);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("notificationSettings")]
        public async Task<ActionResult<NotificationSettings>> GetNotificationSettings(
            CancellationToken cancellationToken)
        {
            var settings = await _notificationSettingsService.GetNotificationSettingsAsync(cancellationToken);
            if (settings == null)
            {
                return NotFound();
            }
            return Ok(settings);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("notificationSettings")]
        public async Task<IActionResult> SaveNotificationSettings(
            [FromBody] NotificationSettingsModel model,
            CancellationToken cancellationToken)
        {
            var settings = new NotificationSettings
            {
                AdminPhoneNumber = model.AdminPhoneNumber,
                BaristaPhoneNumber = model.BaristaPhoneNumber,
                TrailerPhoneNumber = model.TrailerPhoneNumber
            };
            await _notificationSettingsService.SaveNotificationSettingsAsync(settings, cancellationToken);
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("notificationSettings")]
        public async Task<IActionResult> UpdateNotificationSettings(
            [FromBody] NotificationSettingsModel model,
            CancellationToken cancellationToken)
        {
            return await SaveNotificationSettings(model, cancellationToken);
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            Console.WriteLine("Ping endpoint was called");
            return Ok("pong");
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("updateOrderStatus/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, CancellationToken cancellationToken)
        {
            var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            await _orderService.UpdateStatus(order);
            if (order.OrderStatus == OrderStatus.ReadyForPickup)
            {
                await _notificationService.SendReadyForPickupNotificationAsync(order, cancellationToken);
            }
            return Ok(new
            {
                message = "Order status updated successfully.",
                orderId = order.Id,
                newStatus = order.OrderStatus.ToString()
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("orders/{id}/notifications")]
        public async Task<IActionResult> GetOrderNotifications(int id, CancellationToken cancellationToken)
        {
            var notifications = await _notificationService.GetNotificationsForOrderAsync(id, cancellationToken);
            var result = notifications.Select(n => new
            {
                n.Id,
                n.EventType,
                n.RecipientRole,
                n.RecipientPhone,
                n.TemplateKey,
                n.Status,
                n.AttemptCount,
                n.ProviderMessageId,
                n.LastError,
                n.CreatedUtc,
                n.SentUtc,
                n.UpdatedUtc
            });
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost("notifications/twilio-status-callback")]
        public async Task<IActionResult> TwilioStatusCallback(
            [FromForm(Name = "MessageSid")] string? messageSid,
            [FromForm(Name = "MessageStatus")] string? messageStatus,
            [FromForm(Name = "ErrorMessage")] string? errorMessage,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(messageSid) || string.IsNullOrWhiteSpace(messageStatus))
            {
                return Ok();
            }

            await _notificationService.UpdateProviderStatusAsync(messageSid, messageStatus, errorMessage, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Export all menu items as JSON for download/backup.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("menu/export")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> ExportMenu()
        {
            var items = await _menuService.GetMenuItemsAsync();
            return Ok(items);
        }

        /// <summary>
        /// Bulk replace menu with uploaded JSON. Replaces all existing items.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("menu/import")]
        public async Task<IActionResult> ImportMenu([FromBody] List<MenuItem> menuItems)
        {
            if (menuItems == null || menuItems.Count == 0)
            {
                return BadRequest("Menu must contain at least one item.");
            }
            await _menuService.BulkReplaceAsync(menuItems);
            return Ok(new { message = "Menu imported successfully.", count = menuItems.Count });
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

        [Authorize(Roles = "Admin")]
        [HttpGet("seed-menu")]
        public async Task<IActionResult> SeedMenuItems(bool confirm = false)
        {

            if (!confirm)
            {
                return BadRequest("Please confirm the operation by passing '?confirm=true' in the query string.");

            }

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
        [StringLength(32)]
        public string? AdminPhoneNumber { get; set; }

        [StringLength(32)]
        public string? BaristaPhoneNumber { get; set; }

        [StringLength(32)]
        public string? TrailerPhoneNumber { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [StringLength(500)]
        public string? Message { get; set; }
    }
}
