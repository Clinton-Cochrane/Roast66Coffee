using System.ComponentModel.DataAnnotations;
using CoffeeShopApi.Security;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class StaffController(StaffAccountService staffAccounts) : ControllerBase
{
    private readonly StaffAccountService _staffAccounts = staffAccounts;

    [Authorize(Roles = StaffRoles.Admin)]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var actor = StaffActor.FromPrincipal(User);
        if (User.HasClaim(StaffClaimTypes.LegacyShared, "true"))
        {
            return Ok(new StaffAccountDto(
                "legacy-shared-admin",
                actor.DisplayName,
                "legacy-shared-admin",
                true,
                [StaffRoles.Admin]));
        }
        var account = actor.UserId == null ? null : await _staffAccounts.FindAsync(actor.UserId);
        return account == null ? Unauthorized() : Ok(account);
    }

    [Authorize(Roles = StaffRoles.Owner)]
    [HttpGet("staff")]
    public async Task<IActionResult> ListStaff() => Ok(await _staffAccounts.ListAsync());

    [Authorize(Roles = StaffRoles.Owner)]
    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request)
    {
        try
        {
            var created = await _staffAccounts.CreateAsync(
                StaffActor.FromPrincipal(User),
                request.DisplayName,
                request.Username,
                request.InitialPassword,
                request.IsOwner);
            return Created($"/api/admin/staff/{created.Id}", created);
        }
        catch (StaffAccountException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = StaffRoles.Owner)]
    [HttpPost("staff/{id}/disable")]
    public Task<IActionResult> Disable(string id) => SetActive(id, false);

    [Authorize(Roles = StaffRoles.Owner)]
    [HttpPost("staff/{id}/enable")]
    public Task<IActionResult> Enable(string id) => SetActive(id, true);

    [Authorize(Roles = StaffRoles.Owner)]
    [HttpPost("staff/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _staffAccounts.ResetPasswordAsync(StaffActor.FromPrincipal(User), id, request.NewPassword);
            return NoContent();
        }
        catch (StaffAccountException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = StaffRoles.Admin)]
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            await _staffAccounts.ChangePasswordAsync(
                StaffActor.FromPrincipal(User),
                request.CurrentPassword,
                request.NewPassword);
            return NoContent();
        }
        catch (StaffAccountException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> SetActive(string id, bool active)
    {
        try
        {
            return Ok(await _staffAccounts.SetActiveAsync(StaffActor.FromPrincipal(User), id, active));
        }
        catch (StaffAccountException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}

public sealed class CreateStaffRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 12)]
    public string InitialPassword { get; set; } = string.Empty;

    public bool IsOwner { get; set; }
}

public sealed class ResetPasswordRequest
{
    [Required, StringLength(200, MinimumLength = 12)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 12)]
    public string NewPassword { get; set; } = string.Empty;
}
