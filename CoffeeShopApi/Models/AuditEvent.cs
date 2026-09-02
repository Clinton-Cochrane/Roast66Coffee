using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models;

[Table("auditevents")]
public sealed class AuditEvent
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("occurredutc")]
    public DateTime OccurredUtc { get; set; }

    [StringLength(450)]
    [Column("actoruserid")]
    public string? ActorUserId { get; set; }

    [Required]
    [StringLength(100)]
    [Column("actordisplayname")]
    public string ActorDisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Column("entitytype")]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Column("entityid")]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    [Column("detailsjson", TypeName = "jsonb")]
    public string DetailsJson { get; set; } = "{}";
}
