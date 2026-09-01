using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Models;

public sealed class StaffUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
