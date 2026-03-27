using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Models;

[Table("staffpushsubscriptions")]
[Index(nameof(Endpoint), IsUnique = true)]
public class StaffPushSubscription
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(2048)]
    [Column("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Column("p256dh")]
    public string P256Dh { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Column("auth")]
    public string Auth { get; set; } = string.Empty;

    [StringLength(100)]
    [Column("useridentifier")]
    public string? UserIdentifier { get; set; }

    [StringLength(512)]
    [Column("useragent")]
    public string? UserAgent { get; set; }

    [Column("createdutc")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Column("updatedutc")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
