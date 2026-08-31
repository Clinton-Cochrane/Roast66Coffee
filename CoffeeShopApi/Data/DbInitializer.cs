namespace CoffeeShopApi.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            SeedMenuItems.SeedIfEmptyAsync(context).GetAwaiter().GetResult();
        }
    }
}
