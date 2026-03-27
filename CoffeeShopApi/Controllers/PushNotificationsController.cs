using System.ComponentModel.DataAnnotations;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/push")]
public class PushNotificationsController : ControllerBase
{
    private readonly StaffPushNotificationService _staffPushNotificationService;

    public PushNotificationsController(StaffPushNotificationService staffPushNotificationService)
    {
        _staffPushNotificationService = staffPushNotificationService;
    }

    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        var key = _staffPushNotificationService.GetPublicKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Push notifications are not configured."
            });
        }

        return Ok(new { publicKey = key });
    }

    [HttpPost("subscriptions")]
    public async Task<IActionResult> UpsertSubscription(
        [FromBody] PushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!_staffPushNotificationService.IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Push notifications are not configured."
            });
        }

        await _staffPushNotificationService.UpsertSubscriptionAsync(
            endpoint: request.Endpoint.Trim(),
            p256dh: request.Keys.P256Dh.Trim(),
            auth: request.Keys.Auth.Trim(),
            userIdentifier: User.Identity?.Name,
            userAgent: Request.Headers.UserAgent.ToString(),
            cancellationToken: cancellationToken);

        return Ok(new { message = "Subscription saved." });
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> RemoveSubscription(
        [FromBody] RemovePushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        await _staffPushNotificationService.RemoveSubscriptionAsync(request.Endpoint.Trim(), cancellationToken);
        return NoContent();
    }
}

public class PushSubscriptionRequest
{
    [Required]
    [StringLength(2048)]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public PushSubscriptionKeys Keys { get; set; } = new();
}

public class PushSubscriptionKeys
{
    [Required]
    [StringLength(256)]
    [JsonPropertyName("p256dh")]
    public string P256Dh { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [JsonPropertyName("auth")]
    public string Auth { get; set; } = string.Empty;
}

public class RemovePushSubscriptionRequest
{
    [Required]
    [StringLength(2048)]
    public string Endpoint { get; set; } = string.Empty;
}
