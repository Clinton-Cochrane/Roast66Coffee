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

        [Required]
        [Column("phonenumber")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
