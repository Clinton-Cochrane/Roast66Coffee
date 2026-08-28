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
            // Look for any existing data.
            if (context.MenuItems.Any())
            {
                return; // DB has been seeded.
            }

            var menuItems = new MenuItem[]
            {
                new MenuItem {Name = "Espresso", Price = 2.50M, Description = "Strong and bold espresso shot", CategoryType = CategoryType.COFFEE },
                new MenuItem {Name = "Latte", Price = 3.50M, Description = "Creamy latte with milk foam", CategoryType = CategoryType.COFFEE },
                new MenuItem {Name = "Cappuccino", Price = 3.00M, Description = "Rich cappuccino with steamed milk", CategoryType = CategoryType.COFFEE },
                new MenuItem {Name = "Americano", Price = 2.75M, Description = "Espresso diluted with hot water", CategoryType = CategoryType.COFFEE },
                new MenuItem {Name = "Mocha", Price = 4.00M, Description = "Chocolate espresso drink with steamed milk", CategoryType = CategoryType.COFFEE },
                new MenuItem {Name = "Energy Drink", Price = 4.00M, Description = "Select Flavor from below", CategoryType = CategoryType.DRINKS },
                new MenuItem {Name = "Lemonade", Price = 4.00M, Description = "Refreshing Iced Lemonade", CategoryType = CategoryType.DRINKS },
                new MenuItem {Name = "Refresher", Price = 4.00M, Description = "Its like water but different", CategoryType = CategoryType.DRINKS },
                
                // Current signature drinks. Prices are provisional until the client confirms them.
                new MenuItem { Name = "RUSTEZ", Price = 4.25M, Description = "Toasted hazelnut, white mocha", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "MRS.BROWNIE", Price = 4.25M, Description = "Coconut, caramel, and chocolate drizzle", CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true },
                new MenuItem { Name = "PHILTHY305", Price = 4.25M, Description = "Vanilla, caramel drizzle", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "MIDNIGHT MCQUEEN", Price = 4.25M, Description = "English toffee, red raspberry, white mocha", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "OFF-ROAD", Price = 4.25M, Description = "Chocolate, cinnamon powder", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "RUSTY MATER", Price = 4.25M, Description = "White mocha", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "MR.BROWNIE", Price = 4.25M, Description = "Banana, chocolate drizzle", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "BURNOUT", Price = 4.25M, Description = "Toasted marshmallows, chocolate drizzle, and cinnamon powder", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "BLACK CHEVY SS", Price = 4.25M, Description = "Red raspberry, blue raspberry", CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true },
                new MenuItem { Name = "DIESEL", Price = 4.25M, Description = "Pomegranate, strawberry, vanilla", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "BLUE FLAME NITRO", Price = 4.25M, Description = "Blue raspberry, coconut", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "CLUTCH STOP", Price = 4.25M, Description = "Strawberry, white chocolate", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "SIDEWAYS RX", Price = 4.25M, Description = "Vanilla, mango puree", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "SUKI 2 FAST", Price = 4.25M, Description = "Strawberry puree", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "BLOWN HEAD GASKET", Price = 5.50M, Description = "4 shots espresso; pick your flavor", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "CHECK ENGINE LIGHT", Price = 7.50M, Description = "6 shots espresso; pick your flavor", CategoryType = CategoryType.SPECIALS },

                // Existing non-conflicting specials
                new MenuItem {Name = "Shitbox LUV Fuel", Price = 5.00M, Description = "Triple espresso with caramel drizzle and guarana syrup", CategoryType = CategoryType.SPECIALS, IsFeaturedOnHome = true },
                new MenuItem { Name = "GTO Grape Energy Boost", Price = 4.50M, Description = "Grape energy drink with lemon and passion fruit", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Red SnotRod Energy Bump", Price = 2.50M, Description = "Cherry, passion fruit, and white peach flavored energy drink", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Green Nova Refresher", Price = 3.75M, Description = "Iced sparkling lime drink with cucumber and mint", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Pink Slip Punch", Price = 3.50M, Description = "Strawberry, watermelon, and lemon punch", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "454 Punch", Price = 4.75M, Description = "Cherry, pomegranate, and lime energy drink", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Tesla", Price = 85.00M, Description = "Room Temp Tap Water", CategoryType = CategoryType.DRINKS },

                // Flavors (New)
                new MenuItem {Name = "Chocolate Shot", Price = 0.50M, Description = "Rich chocolate flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Vanilla Shot", Price = 0.50M, Description = "Classic vanilla flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Coconut Shot", Price = 0.50M, Description = "Tropical coconut flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Caramel Shot", Price = 0.50M, Description = "Sweet caramel flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Guarana Shot", Price = 0.50M, Description = "Energy-boosting guarana", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Blueberry Shot", Price = 0.50M, Description = "Fresh blueberry flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Grape Shot", Price = 0.50M, Description = "Juicy grape flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Lemon Shot", Price = 0.50M, Description = "Zesty lemon flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Passion Fruit Shot", Price = 0.50M, Description = "Exotic passion fruit flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Red Raspberry Shot", Price = 0.50M, Description = "Tangy red raspberry flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Pomegranate Shot", Price = 0.50M, Description = "Sweet pomegranate flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Cherry Shot", Price = 0.50M, Description = "Bold cherry flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "White Peach Shot", Price = 0.50M, Description = "Delicate white peach flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Lime Shot", Price = 0.50M, Description = "Refreshing lime flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Cucumber Shot", Price = 0.50M, Description = "Cool cucumber flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Mint Shot", Price = 0.50M, Description = "Cooling mint flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Strawberry Shot", Price = 0.50M, Description = "Sweet strawberry flavor", CategoryType = CategoryType.FLAVORS },
                new MenuItem {Name = "Watermelon Shot", Price = 0.50M, Description = "Juicy watermelon flavor", CategoryType = CategoryType.FLAVORS },
            };
            foreach (MenuItem item in menuItems)
            {
                context.MenuItems.Add(item);
            }
            context.SaveChanges();
        }
    }
}
