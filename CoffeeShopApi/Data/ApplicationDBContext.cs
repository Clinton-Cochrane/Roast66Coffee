// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using CoffeeShopApi.Models;

namespace CoffeeShopApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<NotificationSettings> NotificationSettings { get; set; }
    }
}
