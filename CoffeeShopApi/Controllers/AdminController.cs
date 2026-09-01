// Controllers/AdminController.cs
using CoffeeShopApi;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using CoffeeShopApi.Security;

namespace CoffeeShopApi.Controllers
{
    /// <summary>
    /// HTTP boundary for staff authentication and shop operations. It translates
    /// service outcomes into API status codes; ordering, menu, retention, and
    /// notification invariants remain in their focused services.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly MenuService _menuService;
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly NotificationSettingsService _notificationSettingsService;
        private readonly SupportEmailService _supportEmailService;
        private readonly NotificationRetentionService _notificationRetentionService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AdminController> _logger;
        private readonly IDefaultMenuProvider _defaultMenuProvider;
        private readonly UserManager<StaffUser> _userManager;
        private readonly SignInManager<StaffUser> _signInManager;
        private readonly StaffTokenService _staffTokenService;

        public const string DefaultMenuResetConfirmation = "RESET DEFAULT MENU";


        public AdminController(
            OrderService orderService,
            MenuService menuService,
            NotificationService notificationService,
            IConfiguration configuration,
            NotificationSettingsService notificationSettingsService,
            SupportEmailService supportEmailService,
            NotificationRetentionService notificationRetentionService,
            IWebHostEnvironment environment,
            ILogger<AdminController> logger,
            IDefaultMenuProvider defaultMenuProvider,
            UserManager<StaffUser> userManager,
            SignInManager<StaffUser> signInManager,
            StaffTokenService staffTokenService)
        {
            _orderService = orderService;
            _menuService = menuService;
            _notificationService = notificationService;
            _configuration = configuration;
            _notificationSettingsService = notificationSettingsService;
            _supportEmailService = supportEmailService;
            _notificationRetentionService = notificationRetentionService;
            _environment = environment;
            _logger = logger;
            _defaultMenuProvider = defaultMenuProvider;
            _userManager = userManager;
            _signInManager = signInManager;
            _staffTokenService = staffTokenService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting("Login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            var user = await _userManager.FindByNameAsync(login.Username.Trim());
            if (user != null && user.IsActive)
            {
                var signIn = await _signInManager.CheckPasswordSignInAsync(
                    user,
                    login.Password,
                    lockoutOnFailure: true);
                if (signIn.Succeeded)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    return Ok(new { token = _staffTokenService.Create(user, roles) });
                }
            }

            if (_configuration.GetValue("Authentication:LegacySharedLoginEnabled", false) &&
                string.Equals(login.Username, _configuration["Admin:Username"], StringComparison.Ordinal) &&
                string.Equals(login.Password, _configuration["Admin:Password"], StringComparison.Ordinal))
            {
                return Ok(new { token = _staffTokenService.CreateLegacy() });
            }

            return Unauthorized();
        }

        [AllowAnonymous]
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

        [Authorize(Roles = "Admin")]
        [HttpGet("credential-settings")]
        public ActionResult<CredentialSettingsInfo> GetCredentialSettingsInfo()
        {
            if (!_configuration.GetValue("Authentication:LegacySharedLoginEnabled", false))
            {
                return NotFound();
            }
            return Ok(new CredentialSettingsInfo
            {
                Username = _configuration["Admin:Username"] ?? string.Empty,
                UsernameEnvKey = "Admin__Username",
                PasswordEnvKey = "Admin__Password",
                UpdateInstructions = "Update these environment variables in your deployment provider and redeploy the API service."
            });
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
        [Authorize(Roles = "Admin")]
        [HttpGet("menu")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> GetMenuItems()
        {
            return Ok(await _menuService.GetAllMenuItemsAsync());
        }

        // Add a new menu item
        [Authorize(Roles = "Admin")]
        [HttpPost("menu")]
        public async Task<ActionResult<MenuItem>> PostMenuItem(MenuItem menuItem)
        {
            var createdItem = await _menuService.CreateMenuItemAsync(menuItem, StaffActor.FromPrincipal(User));
            return CreatedAtAction(nameof(GetMenuItems), new { id = createdItem.Id }, createdItem);
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

            var updated = await _menuService.UpdateMenuItemAsync(menuItem, StaffActor.FromPrincipal(User));
            if (!updated) return NotFound();

            return NoContent();
        }

        public sealed record HomepageSpecialSelectionRequest(bool IsSelected);
        public sealed record MenuSpecialSelectionRequest(bool IsSelected);
        public sealed record PromotionUpdateRequest(string? Promotion);

        [Authorize(Roles = "Admin")]
        [HttpPut("menu/{id}/homepage-special")]
        public async Task<IActionResult> SetHomepageSpecial(
            int id,
            [FromBody] HomepageSpecialSelectionRequest request)
        {
            var result = await _menuService.SetHomepageSpecialAsync(
                id,
                request.IsSelected,
                HttpContext.RequestAborted,
                StaffActor.FromPrincipal(User));
            return result switch
            {
                HomepageSpecialSelectionResult.Updated => NoContent(),
                HomepageSpecialSelectionResult.NotFound => NotFound(),
                HomepageSpecialSelectionResult.Unavailable => Conflict(new
                {
                    message = "Archived menu items cannot be featured."
                }),
                HomepageSpecialSelectionResult.LimitReached => Conflict(new
                {
                    message = $"Only {MenuService.MaxHomepageSpecials} homepage specials can be selected."
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("menu/{id}/menu-special")]
        public async Task<IActionResult> SetMenuSpecial(int id, [FromBody] MenuSpecialSelectionRequest request) =>
            await _menuService.SetMenuSpecialAsync(id, request.IsSelected, StaffActor.FromPrincipal(User)) ? NoContent() : NotFound();

        [Authorize(Roles = "Admin")]
        [HttpPut("menu/{id}/promotion")]
        public async Task<IActionResult> SetPromotion(int id, [FromBody] PromotionUpdateRequest request)
        {
            var result = await _menuService.SetPromotionAsync(id, request.Promotion, StaffActor.FromPrincipal(User));
            return result switch
            {
                MenuItemUpdateResult.Updated => NoContent(),
                MenuItemUpdateResult.NotFound => NotFound(),
                MenuItemUpdateResult.InvalidPromotion => BadRequest(new { message = "Use a valid discount such as $2 or 32%. The discounted price must be at least $0.01." }),
                _ => StatusCode(500)
            };
        }

        // Delete a menu item
        [Authorize(Roles = "Admin")]
        [HttpDelete("menu/{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            return await _menuService.DeleteMenuItemAsync(id, StaffActor.FromPrincipal(User)) ? NoContent() : NotFound();
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("menu/{id}/archive")]
        public async Task<IActionResult> ArchiveMenuItem(int id) =>
            await _menuService.ArchiveMenuItemAsync(id, StaffActor.FromPrincipal(User)) ? NoContent() : NotFound();

        [Authorize(Roles = "Admin")]
        [HttpPut("menu/{id}/restore")]
        public async Task<IActionResult> RestoreMenuItem(int id) =>
            await _menuService.RestoreMenuItemAsync(id, StaffActor.FromPrincipal(User)) ? NoContent() : NotFound();

        [Authorize(Roles = "Admin")]
        // Get the bounded operational order history.
        [HttpGet("orders")]
        public async Task<ActionResult<AdminOrderHistoryResponse>> GetOrders(
            [FromQuery] AdminOrderHistoryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _orderService.GetOrderHistoryAsync(
                request,
                DateTime.UtcNow,
                cancellationToken));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("orders/new-count")]
        public async Task<ActionResult<object>> GetNewOrdersCount([FromQuery] DateTime since)
        {
            var count = await _orderService.GetCountSinceAsync(since.ToUniversalTime());
            return Ok(new { count });
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
                TrailerPhoneNumber = model.TrailerPhoneNumber,
                AdminEmail = model.AdminEmail,
                BaristaEmail = model.BaristaEmail,
                TrailerEmail = model.TrailerEmail,
                SmsFromAddress = model.SmsFromAddress
            };
            await _notificationSettingsService.SaveNotificationSettingsAsync(
                settings,
                cancellationToken,
                StaffActor.FromPrincipal(User));
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

        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            Console.WriteLine("Ping endpoint was called");
            return Ok("pong");
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("updateOrderStatus/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(
            int id,
            [FromBody] AdvanceOrderStatusRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _orderService.AdvanceStatusAsync(
                id,
                request.ExpectedStatus!.Value,
                StaffActor.FromPrincipal(User),
                cancellationToken);
            if (result.Outcome == OrderStatusAdvanceOutcome.NotFound)
            {
                return NotFound(new { code = "order_not_found", message = result.Message });
            }
            if (result.Outcome == OrderStatusAdvanceOutcome.InvalidExpectedStatus)
            {
                return BadRequest(new { code = "invalid_order_status", message = result.Message });
            }
            if (result.Outcome is OrderStatusAdvanceOutcome.Conflict or
                OrderStatusAdvanceOutcome.InvalidCurrentStatus)
            {
                return Conflict(new
                {
                    code = "order_status_conflict",
                    message = result.Message,
                    orderId = result.OrderId,
                    expectedStatus = result.ExpectedStatus.ToString(),
                    currentStatus = result.Order?.OrderStatus.ToString()
                });
            }

            var order = result.Order!;
            if (result.Changed && order.OrderStatus == OrderStatus.ReadyForPickup)
            {
                await _notificationService.SendReadyForPickupNotificationAsync(order, cancellationToken);
            }
            return Ok(new
            {
                message = result.Message,
                orderId = order.Id,
                newStatus = order.OrderStatus.ToString(),
                changed = result.Changed,
                replayed = result.Outcome == OrderStatusAdvanceOutcome.Replayed,
                terminal = result.Outcome == OrderStatusAdvanceOutcome.Terminal
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
                n.Channel,
                n.Provider,
                n.RecipientRole,
                n.RecipientPhone,
                n.RecipientEmail,
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

        [Authorize(Roles = "Admin")]
        [HttpPost("notifications/purge-logs")]
        [HttpPost("notifications/purge-email-logs")]
        public async Task<IActionResult> PurgeNotificationLogs(CancellationToken cancellationToken)
        {
            await _notificationRetentionService.PurgeNotificationsOlderThanAsync(
                DateTime.UtcNow.AddDays(-NotificationRetentionService.RetentionDays),
                cancellationToken,
                StaffActor.FromPrincipal(User));

            return NoContent();
        }

        /// <summary>
        /// Export all menu items as JSON for download/backup.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("menu/export")]
        public async Task<ActionResult<IEnumerable<MenuItem>>> ExportMenu()
        {
            var items = await _menuService.GetAllMenuItemsAsync();
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
            await _menuService.BulkReplaceAsync(
                menuItems,
                HttpContext.RequestAborted,
                StaffActor.FromPrincipal(User));
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

        public sealed record MenuResetRequest(string? Confirmation);

        [Authorize(Roles = "Admin")]
        [HttpPost("menu/reset-to-defaults")]
        public async Task<IActionResult> ResetMenuToDefaults(
            [FromBody] MenuResetRequest? request,
            CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
            {
                return NotFound();
            }

            if (!string.Equals(
                    request?.Confirmation,
                    DefaultMenuResetConfirmation,
                    StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    message = $"Confirmation must exactly match '{DefaultMenuResetConfirmation}'."
                });
            }

            try
            {
                var summary = await _menuService.BulkReplaceAsync(
                    _defaultMenuProvider.GetMenuItems(),
                    cancellationToken,
                    StaffActor.FromPrincipal(User),
                    "menu.reset_to_defaults");
                return Ok(new
                {
                    message = "Default menu reset completed.",
                    summary.PreviousItemCount,
                    summary.NewItemCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Default menu reset failed. Failure type: {FailureType}.",
                    ex.GetType().Name);
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Menu reset failed",
                    detail: "The default menu could not be reset. The existing menu was left unchanged.");
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

        [EmailAddress]
        [StringLength(320)]
        public string? AdminEmail { get; set; }

        [EmailAddress]
        [StringLength(320)]
        public string? BaristaEmail { get; set; }

        [EmailAddress]
        [StringLength(320)]
        public string? TrailerEmail { get; set; }

        [StringLength(32)]
        public string? SmsFromAddress { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [StringLength(500)]
        public string? Message { get; set; }
    }

    public class CredentialSettingsInfo
    {
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [StringLength(100)]
        public string UsernameEnvKey { get; set; } = string.Empty;

        [StringLength(100)]
        public string PasswordEnvKey { get; set; } = string.Empty;

        [StringLength(500)]
        public string UpdateInstructions { get; set; } = string.Empty;
    }
}
