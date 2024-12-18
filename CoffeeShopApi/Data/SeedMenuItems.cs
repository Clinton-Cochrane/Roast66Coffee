using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;



namespace CoffeeShopApi.Data
{
    public static class SeedMenuItems
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // List of drinks to insert/update
            var drinks = new List<MenuItem>
            {
               
                
                // Drinks
                new MenuItem { Name = "Espresso", Price = 2.50M, Description = "Strong and bold espresso shot", CategoryType = CategoryType.COFFEE },
                new MenuItem { Name = "Latte", Price = 3.50M, Description = "Creamy latte with milk foam", CategoryType = CategoryType.COFFEE },
                new MenuItem { Name = "Cappuccino", Price = 3.00M, Description = "Rich cappuccino with steamed milk", CategoryType = CategoryType.COFFEE },
                new MenuItem { Name = "Americano", Price = 2.75M, Description = "Espresso diluted with hot water", CategoryType = CategoryType.COFFEE },
                new MenuItem { Name = "Mocha", Price = 4.00M, Description = "Chocolate espresso drink with steamed milk", CategoryType = CategoryType.COFFEE },

                // Drinks
                new MenuItem { Name = "Energy Drink", Price = 4.00M, Description = "Select Flavor from below", CategoryType = CategoryType.DRINKS },
                new MenuItem { Name = "Lemonade", Price = 4.00M, Description = "Refreshing Iced Lemonade", CategoryType = CategoryType.DRINKS },
                new MenuItem { Name = "Refresher", Price = 4.00M, Description = "It's like water but different", CategoryType = CategoryType.DRINKS },
                new MenuItem { Name = "Tesla", Price = 85.00M, Description = "Room Temp Tap Water", CategoryType = CategoryType.DRINKS },

                // Specials
                new MenuItem { Name = "Mr. Brownie Shaken Espresso", Price = 4.15M, Description = "Shaken espresso with brown sugar, cinnamon powder, and vanilla cold foam", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Mrs. Brownie Latte", Price = 7.23M, Description = "Iced coconut and caramel latte", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Shitbox LUV Fuel", Price = 5.00M, Description = "Triple espresso with caramel drizzle and guarana syrup", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Blue Flame Nitro", Price = 5.25M, Description = "Nitro cold brew with sweet cream and blueberry syrup", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "GTO Grape Energy Boost", Price = 4.50M, Description = "Grape energy drink with lemon and passion fruit", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Black SS Lemonade", Price = 2.50M, Description = "Red raspberry, pomegranate, and bubbly lemonade", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Red SnotRod Energy Bump", Price = 2.50M, Description = "Cherry, passion fruit, and white peach flavored energy drink", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Green Nova Refresher", Price = 3.75M, Description = "Iced sparkling lime drink with cucumber and mint", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "Pink Slip Punch", Price = 3.50M, Description = "Strawberry, watermelon, and lemon punch", CategoryType = CategoryType.SPECIALS },
                new MenuItem { Name = "454 Punch", Price = 4.75M, Description = "Cherry, pomegranate, and lime energy drink", CategoryType = CategoryType.SPECIALS },

                // Flavors
                new MenuItem { Name = "Chocolate Shot", Price = 0.50M, Description = "Rich chocolate flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Vanilla Shot", Price = 0.50M, Description = "Classic vanilla flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Coconut Shot", Price = 0.50M, Description = "Tropical coconut flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Caramel Shot", Price = 0.50M, Description = "Sweet caramel flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Guarana Shot", Price = 0.50M, Description = "Energy-boosting guarana shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Blueberry Shot", Price = 0.50M, Description = "Fresh blueberry flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Grape Shot", Price = 0.50M, Description = "Juicy grape flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Lemon Shot", Price = 0.50M, Description = "Zesty lemon flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Passion Fruit Shot", Price = 0.50M, Description = "Exotic passion fruit flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Red Raspberry Shot", Price = 0.50M, Description = "Tangy red raspberry flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Pomegranate Shot", Price = 0.50M, Description = "Sweet pomegranate flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Cherry Shot", Price = 0.50M, Description = "Bold cherry flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "White Peach Shot", Price = 0.50M, Description = "Delicate white peach flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Lime Shot", Price = 0.50M, Description = "Refreshing lime flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Cucumber Shot", Price = 0.50M, Description = "Cool cucumber flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Mint Shot", Price = 0.50M, Description = "Cooling mint flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Strawberry Shot", Price = 0.50M, Description = "Sweet strawberry flavor shot", CategoryType = CategoryType.FLAVORS },
                new MenuItem { Name = "Watermelon Shot", Price = 0.50M, Description = "Juicy watermelon flavor shot", CategoryType = CategoryType.FLAVORS }


                // Add more drinks as needed
            };

            // Loop through each drink and add or update it
            try
            {
                context.MenuItems.RemoveRange(context.MenuItems);
                await context.SaveChangesAsync();

                await context.MenuItems.AddRangeAsync(context.MenuItems);
                await context.SaveChangesAsync();
                Console.WriteLine("Seeding completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during seeding: {ex.Message}");
                throw;
            }
        }
    }
}
