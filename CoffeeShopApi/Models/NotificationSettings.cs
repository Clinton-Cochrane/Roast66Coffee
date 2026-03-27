// Models/NotificationSettings.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoffeeShopApi.Models
{
    [Table("notificationsettings")]
    public class NotificationSettings
    {
        [Key]
        public int Id { get; set; }

        [StringLength(32)]
        [Column("adminphonenumber")]
        public string? AdminPhoneNumber { get; set; }

        [StringLength(32)]
        [Column("baristaphonenumber")]
        public string? BaristaPhoneNumber { get; set; }

        [StringLength(32)]
        [Column("trailerphonenumber")]
        public string? TrailerPhoneNumber { get; set; }

        [StringLength(320)]
        [Column("adminemail")]
        public string? AdminEmail { get; set; }

        [StringLength(320)]
        [Column("baristaemail")]
        public string? BaristaEmail { get; set; }

        [StringLength(320)]
        [Column("traileremail")]
        public string? TrailerEmail { get; set; }

        [StringLength(32)]
        [Column("twiliofromphonenumber")]
        public string? TwilioFromPhoneNumber { get; set; }
    }
}
