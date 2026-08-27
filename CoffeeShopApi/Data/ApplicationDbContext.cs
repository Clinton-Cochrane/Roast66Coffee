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
        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>()
                .Property(order => order.TrackingToken)
                .HasMaxLength(43)
                .IsRequired();
            modelBuilder.Entity<Order>()
                .HasIndex(order => order.TrackingToken)
                .IsUnique();
            modelBuilder.Entity<Payment>()
                .HasIndex(payment => new { payment.Provider, payment.IdempotencyKey });
            modelBuilder.Entity<Payment>()
                .HasIndex(payment => new { payment.Provider, payment.ProviderCheckoutId })
                .IsUnique();
            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.Order)
                .WithMany()
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
