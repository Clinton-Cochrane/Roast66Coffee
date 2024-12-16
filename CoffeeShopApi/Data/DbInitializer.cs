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
             new MenuItem { Id = 1, Name = "Espresso", Price = 2.50M, Description = "Strong and bold espresso shot", CategoryType = CategoryType.Coffee },
                new MenuItem { Id = 2, Name = "Latte", Price = 3.50M, Description = "Creamy latte with milk foam", CategoryType = CategoryType.Coffee },
                new MenuItem { Id = 3, Name = "Cappuccino", Price = 3.00M, Description = "Rich cappuccino with steamed milk", CategoryType = CategoryType.Coffee },
                new MenuItem { Id = 4, Name = "Americano", Price = 2.75M, Description = "Espresso diluted with hot water", CategoryType = CategoryType.Coffee },
                new MenuItem { Id = 5, Name = "Mocha", Price = 4.00M, Description = "Chocolate espresso drink with steamed milk", CategoryType = CategoryType.Coffee },
                new MenuItem { Id = 5, Name = "Energy Drink", Price = 4.00M, Description = "Select Flavor from below", CategoryType = CategoryType.Drink },
                new MenuItem { Id = 5, Name = "Lemonade", Price = 4.00M, Description = "Refreshing Iced Lemonade", CategoryType = CategoryType.Drink },
                new MenuItem { Id = 5, Name = "Refresher", Price = 4.00M, Description = "Its like water but different", CategoryType = CategoryType.Drink },
                
                // Specials
                new MenuItem { Id = 6, Name = "Mr. Brownie Shaken Espresso", Price = 4.15M, Description = "Shaken espresso with brown sugar, cinnamon powder, and vanilla cold foam", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 7, Name = "Mrs. Brownie Latte", Price = 7.23M, Description = "Iced coconut and caramel latte", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 8, Name = "Shitbox LUV Fuel", Price = 5.00M, Description = "Triple espresso with caramel drizzle and guarana syrup", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 9, Name = "Blue Flame Nitro", Price = 5.25M, Description = "Nitro cold brew with sweet cream and blueberry syrup", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 10, Name = "GTO Grape Energy Boost", Price = 4.50M, Description = "Grape energy drink with lemon and passion fruit", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 11, Name = "Black SS Lemonade", Price = 2.50M, Description = "Red raspberry, pomegranate, and bubbly lemonade", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 12, Name = "Red SnotRod Energy Bump", Price = 2.50M, Description = "Cherry, passion fruit, and white peach flavored energy drink", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 13, Name = "Green Nova Refresher", Price = 3.75M, Description = "Iced sparkling lime drink with cucumber and mint", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 14, Name = "Pink Slip Punch", Price = 3.50M, Description = "Strawberry, watermelon, and lemon punch", CategoryType = CategoryType.Specials },
                new MenuItem { Id = 15, Name = "454 Punch", Price = 4.75M, Description = "Cherry, pomegranate, and lime energy drink", CategoryType = CategoryType.Specials },

                // Flavors (New)
                new MenuItem { Id = 11, Name = "Chocolate Shot", Price = 0.50M, Description = "Rich chocolate flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 12, Name = "Vanilla Shot", Price = 0.50M, Description = "Classic vanilla flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 13, Name = "Coconut Shot", Price = 0.50M, Description = "Tropical coconut flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 14, Name = "Caramel Shot", Price = 0.50M, Description = "Sweet caramel flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 15, Name = "Guarana Shot", Price = 0.50M, Description = "Energy-boosting guarana shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 16, Name = "Blueberry Shot", Price = 0.50M, Description = "Fresh blueberry flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 17, Name = "Grape Shot", Price = 0.50M, Description = "Juicy grape flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 18, Name = "Lemon Shot", Price = 0.50M, Description = "Zesty lemon flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 19, Name = "Passion Fruit Shot", Price = 0.50M, Description = "Exotic passion fruit flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 20, Name = "Red Raspberry Shot", Price = 0.50M, Description = "Tangy red raspberry flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 21, Name = "Pomegranate Shot", Price = 0.50M, Description = "Sweet pomegranate flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 22, Name = "Cherry Shot", Price = 0.50M, Description = "Bold cherry flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 23, Name = "White Peach Shot", Price = 0.50M, Description = "Delicate white peach flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 24, Name = "Lime Shot", Price = 0.50M, Description = "Refreshing lime flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 25, Name = "Cucumber Shot", Price = 0.50M, Description = "Cool cucumber flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 26, Name = "Mint Shot", Price = 0.50M, Description = "Cooling mint flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 27, Name = "Strawberry Shot", Price = 0.50M, Description = "Sweet strawberry flavor shot", CategoryType = CategoryType.Flavors },
                new MenuItem { Id = 28, Name = "Watermelon Shot", Price = 0.50M, Description = "Juicy watermelon flavor shot", CategoryType = CategoryType.Flavors }
            };
            foreach (MenuItem item in menuItems)
            {
                context.MenuItems.Add(item);
            }
            context.SaveChanges();
        }
    }
}