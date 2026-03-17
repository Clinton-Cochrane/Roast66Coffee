using Microsoft.AspNetCore.Mvc;

namespace CoffeeShopApi.Controllers;

/// <summary>
/// Health check endpoint for load balancers and monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns 200 OK when the API is running. Use for liveness probes.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
