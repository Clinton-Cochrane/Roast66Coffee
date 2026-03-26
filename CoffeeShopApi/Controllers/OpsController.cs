using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpsController : ControllerBase
{
    private readonly KeepAliveStateStore _stateStore;
    private readonly IConfiguration _configuration;

    public OpsController(KeepAliveStateStore stateStore, IConfiguration configuration)
    {
        _stateStore = stateStore;
        _configuration = configuration;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("keepalive/heartbeat")]
    public IActionResult RecordHeartbeat([FromBody] KeepAliveHeartbeatRequest request)
    {
        _stateStore.RecordHeartbeat(request.Source);
        return Accepted(new { status = "ok", timestamp = DateTime.UtcNow });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("keepalive/status")]
    public IActionResult GetStatus()
    {
        var (lastHeartbeatUtc, source) = _stateStore.Snapshot();
        var activeWindowMinutes = Math.Max(1, _configuration.GetValue("KeepAlive:ActiveWindowMinutes", 10));
        var isActive = lastHeartbeatUtc > DateTime.MinValue &&
            DateTime.UtcNow - lastHeartbeatUtc <= TimeSpan.FromMinutes(activeWindowMinutes);

        return Ok(new
        {
            enabled = _configuration.GetValue("KeepAlive:Enabled", false),
            isActive,
            source,
            lastHeartbeatUtc
        });
    }
}
