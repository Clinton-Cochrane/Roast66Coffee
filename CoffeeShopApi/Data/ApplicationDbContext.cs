// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Models;
using CoffeeShopApi.Models.Payments;

namespace CoffeeShopApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MenuItem> MenuItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<NotificationSettings> NotificationSettings { get; set; } = null!;
        public DbSet<NotificationMessage> NotificationMessages { get; set; } = null!;
        public DbSet<StaffPushSubscription> StaffPushSubscriptions { get; set; } = null!;
        public DbSet<PaymentCheckoutDraft> PaymentCheckoutDrafts { get; set; } = null!;
    }
}

