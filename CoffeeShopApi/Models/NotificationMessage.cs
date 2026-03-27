using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Models;

[Table("notificationmessages")]
[Index(nameof(DedupKey), IsUnique = true)]
public class NotificationMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(64)]
    [Column("eventtype")]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    [Column("recipientrole")]
    public string RecipientRole { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    [Column("recipientphone")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    [Column("templatekey")]
    public string TemplateKey { get; set; } = string.Empty;

    [Column("orderid")]
    public int? OrderId { get; set; }

    [Required]
    [Column("payloadjson")]
    public string PayloadJson { get; set; } = "{}";

    [Required]
    [StringLength(24)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [StringLength(64)]
    [Column("providermessageid")]
    public string? ProviderMessageId { get; set; }

    [StringLength(2000)]
    [Column("lasterror")]
    public string? LastError { get; set; }

    [Column("attemptcount")]
    public int AttemptCount { get; set; }

    [StringLength(128)]
    [Column("dedupkey")]
    public string DedupKey { get; set; } = string.Empty;

    [Column("createdutc")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Column("updatedutc")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    [Column("sentutc")]
    public DateTime? SentUtc { get; set; }
}
