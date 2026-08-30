namespace CoffeeShopApi.Services;

public sealed class UnavailableMenuItemsException : Exception
{
    public UnavailableMenuItemsException()
        : base("One or more selected menu items are unavailable.")
    {
    }
}
