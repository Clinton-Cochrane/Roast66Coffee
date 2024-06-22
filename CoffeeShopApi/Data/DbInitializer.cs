// Data/DbInitializer.cs
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CoffeeShopApi.Data
{

    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Ensure the database is created.
            context.Database.EnsureCreated();

            // Look for any existing data.
            if (context.MenuItems.Any())
            {
                return; // DB has been seeded.
            }

            var menuItems = new MenuItem[]
            {
            new MenuItem{Name="Espresso", Price=2.50M, Description="Strong and bold espresso shot"},
            new MenuItem{Name="Latte", Price=3.50M, Description="Creamy latte with milk foam"},
            new MenuItem{Name="Cappuccino", Price=3.00M, Description="Rich cappuccino with steamed milk"},
            };

            foreach (MenuItem item in menuItems)
            {
                context.MenuItems.Add(item);
            }
            context.SaveChanges();
        }
    }
}