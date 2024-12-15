using CoffeeShopApi.Models;
using System.Threading.Tasks;
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
                new MenuItem { Name = "Espresso", Price = 2.50M, Description = "Strong and bold espresso shot" },
                new MenuItem { Name = "Latte", Price = 3.50M, Description = "Creamy latte with milk foam" },
                new MenuItem { Name = "Cappuccino", Price = 3.00M, Description = "Rich cappuccino with steamed milk" },
                new MenuItem { Name = "Americano", Price = 2.75M, Description = "Espresso diluted with hot water" },
                new MenuItem { Name = "Mocha", Price = 4.00M, Description = "Chocolate espresso drink with steamed milk" },
                new MenuItem { Name = "Macchiato", Price = 3.25M, Description = "Espresso with a small amount of milk" },
                new MenuItem { Name = "Flat White", Price = 3.75M, Description = "Velvety smooth espresso with steamed milk" },
                new MenuItem { Name = "Mr. Brownie Shaken Espresso", Price = 4.15M, Description = "Shaken espresso with brown sugar, cinnamon powder, and vanilla cold foam" },
                new MenuItem { Name = "Mrs. Brownie Latte", Price = 7.23M, Description = "Iced coconut and caramel latte" },
                new MenuItem { Name = "Black SS Lemonade", Price = 2.50M, Description = "Red raspberry, pomegranate, and bubbly lemonade" },
                new MenuItem { Name = "Red SnotRod Energy Bump", Price = 2.50M, Description = "Cherry, passion fruit, and white peach flavored energy drink" },
                new MenuItem { Name = "Brown S10 Turbo", Price = 4.00M, Description = "Double shot espresso with dark chocolate and chili kick" },
                new MenuItem { Name = "Green Nova Refresher", Price = 3.75M, Description = "Iced sparkling lime drink with cucumber and mint" },
                new MenuItem { Name = "Shitbox LUV Fuel", Price = 5.00M, Description = "Triple espresso with caramel drizzle and guarana syrup" },
                new MenuItem { Name = "Yellow Cuda Cold Brew", Price = 4.25M, Description = "Cold brew with honey and vanilla cream" },
                new MenuItem { Name = "Midnight Charger Mocha", Price = 4.50M, Description = "Dark chocolate mocha with espresso and hazelnut" },
                new MenuItem { Name = "Hot Rod Cinnamon Chai", Price = 4.00M, Description = "Spiced chai latte with extra cinnamon" },
                new MenuItem { Name = "Blue Flame Nitro", Price = 5.25M, Description = "Nitro cold brew with sweet cream and blueberry syrup" },
                new MenuItem { Name = "GTO Grape Energy Boost", Price = 4.50M, Description = "Grape energy drink with lemon and passion fruit" },
                new MenuItem { Name = "Track Hawk Toffee Latte", Price = 4.75M, Description = "Toffee latte with whipped cream and caramel drizzle" },
                new MenuItem { Name = "Pink Slip Punch", Price = 3.50M, Description = "Strawberry, watermelon, and lemon punch" },
                new MenuItem { Name = "LS Swap Cold Brew", Price = 4.75M, Description = "Smooth, nitro-infused cold brew with caramel and vanilla." },
                new MenuItem { Name = "Big Block Brew", Price = 5.00M, Description = "Extra-strong dark roast coffee with a double shot of espresso." },
                new MenuItem { Name = "Small Block Special", Price = 4.00M, Description = "Classic cappuccino with cocoa dusting." },
                new MenuItem { Name = "Chevelle Cream Dream", Price = 4.50M, Description = "Vanilla latte with caramel swirl and whipped cream." },
                new MenuItem { Name = "Black Tire Roasted Camaro", Price = 5.25M, Description = "Black Coffee chocolate and hazelnut." },
                new MenuItem { Name = "454 Punch", Price = 4.75M, Description = "Cherry, pomegranate, and lime energy drink." },
                new MenuItem { Name = "Rat Rod Roast", Price = 4.25M, Description = "Dark espresso with molasses and cinnamon." },
                new MenuItem { Name = "SS Super Shot", Price = 3.50M, Description = "Triple espresso shot with raw sugar." },
                new MenuItem { Name = "Z28 Zinger", Price = 4.25M, Description = "Iced lemon and ginger tea with sparkling water." },
                new MenuItem { Name = "427 Thunderbolt", Price = 5.00M, Description = "Espresso with dark chocolate and cayenne." },
                // Add more drinks as needed
            };

            // Loop through each drink and add or update it
            try
            {
                foreach (var drink in drinks)
                {
                    var existingDrink = await context.MenuItems.FirstOrDefaultAsync(m => m.Name == drink.Name);

                    if (existingDrink == null)
                    {
                        context.MenuItems.Add(drink);
                    }
                    else
                    {
                        existingDrink.Price = drink.Price;
                        existingDrink.Description = drink.Description;
                    }
                }

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
