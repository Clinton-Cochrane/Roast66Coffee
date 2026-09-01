using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoffeeShopApi.Services;

public sealed record StaffAccountDto(
    string Id,
    string DisplayName,
    string Username,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed class StaffAccountException(string message) : InvalidOperationException(message);

public sealed class StaffAccountService(
    ApplicationDbContext context,
    UserManager<StaffUser> userManager,
    RoleManager<IdentityRole> roleManager,
    AuditEventFactory auditEvents)
{
    private readonly ApplicationDbContext _context = context;
    private readonly UserManager<StaffUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly AuditEventFactory _auditEvents = auditEvents;

    public async Task<IReadOnlyList<StaffAccountDto>> ListAsync()
    {
        var users = await _userManager.Users.OrderBy(user => user.DisplayName).ToListAsync();
        var result = new List<StaffAccountDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(await ToDtoAsync(user));
        }
        return result;
    }

    public async Task<StaffAccountDto?> FindAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user == null ? null : await ToDtoAsync(user);
    }

    public async Task<StaffAccountDto> CreateAsync(
        StaffActor actor,
        string displayName,
        string username,
        string password,
        bool isOwner)
    {
        await EnsureRolesAsync();
        await using var transaction = await BeginTransactionAsync();
        var user = new StaffUser
        {
            UserName = username.Trim(),
            DisplayName = displayName.Trim(),
            IsActive = true
        };
        EnsureSucceeded(await _userManager.CreateAsync(user, password));
        var roles = isOwner
            ? new[] { StaffRoles.Admin, StaffRoles.Owner }
            : new[] { StaffRoles.Admin };
        EnsureSucceeded(await _userManager.AddToRolesAsync(user, roles));
        _auditEvents.Add(actor, "staff.created", "staff", user.Id, new
        {
            user.DisplayName,
            Username = user.UserName,
            Roles = roles
        });
        await _context.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return await ToDtoAsync(user);
    }

    public async Task<StaffAccountDto> SetActiveAsync(StaffActor actor, string id, bool isActive)
    {
        var user = await RequireUserAsync(id);
        if (!isActive && string.Equals(actor.UserId, user.Id, StringComparison.Ordinal))
        {
            throw new StaffAccountException("You cannot disable your current account.");
        }
        if (!isActive && user.IsActive && await _userManager.IsInRoleAsync(user, StaffRoles.Owner))
        {
            var owners = await _userManager.GetUsersInRoleAsync(StaffRoles.Owner);
            if (owners.Count(owner => owner.IsActive) <= 1)
            {
                throw new StaffAccountException("The final active Owner cannot be disabled.");
            }
        }

        await using var transaction = await BeginTransactionAsync();
        user.IsActive = isActive;
        EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user));
        if (!isActive)
        {
            var subscriptions = await _context.StaffPushSubscriptions
                .Where(subscription => subscription.StaffUserId == user.Id)
                .ToListAsync();
            _context.StaffPushSubscriptions.RemoveRange(subscriptions);
        }
        _auditEvents.Add(
            actor,
            isActive ? "staff.enabled" : "staff.disabled",
            "staff",
            user.Id,
            new { user.DisplayName });
        await _context.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return await ToDtoAsync(user);
    }

    public async Task ResetPasswordAsync(StaffActor actor, string id, string password)
    {
        var user = await RequireUserAsync(id);
        await using var transaction = await BeginTransactionAsync();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await _userManager.ResetPasswordAsync(user, token, password));
        _auditEvents.Add(actor, "staff.password_reset", "staff", user.Id, new { user.DisplayName });
        await _context.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
    }

    public async Task ChangePasswordAsync(
        StaffActor actor,
        string currentPassword,
        string newPassword)
    {
        var user = actor.UserId == null ? null : await _userManager.FindByIdAsync(actor.UserId);
        if (user == null) throw new StaffAccountException("The staff account was not found.");
        await using var transaction = await BeginTransactionAsync();
        EnsureSucceeded(await _userManager.ChangePasswordAsync(user, currentPassword, newPassword));
        _auditEvents.Add(actor, "staff.password_changed", "staff", user.Id);
        await _context.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in new[] { StaffRoles.Admin, StaffRoles.Owner })
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await _roleManager.CreateAsync(new IdentityRole(role)));
            }
        }
    }

    private async Task<StaffUser> RequireUserAsync(string id) =>
        await _userManager.FindByIdAsync(id) ??
        throw new StaffAccountException("The staff account was not found.");

    private async Task<StaffAccountDto> ToDtoAsync(StaffUser user) =>
        new(
            user.Id,
            user.DisplayName,
            user.UserName ?? string.Empty,
            user.IsActive,
            (await _userManager.GetRolesAsync(user)).OrderBy(role => role).ToArray());

    private async Task<IDbContextTransaction?> BeginTransactionAsync() =>
        _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new StaffAccountException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
