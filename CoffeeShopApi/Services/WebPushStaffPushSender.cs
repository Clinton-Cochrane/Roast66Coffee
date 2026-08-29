using CoffeeShopApi.Models;
using WebPush;

namespace CoffeeShopApi.Services;

public interface IStaffPushSender
{
    Task SendAsync(
        StaffPushSubscription subscription,
        string payload,
        CancellationToken cancellationToken);
}

internal sealed class WebPushStaffPushSender : IStaffPushSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public WebPushStaffPushSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SendAsync(
        StaffPushSubscription subscription,
        string payload,
        CancellationToken cancellationToken)
    {
        var vapidDetails = new VapidDetails(
            _configuration["Push:Subject"]!,
            _configuration["Push:VapidPublicKey"]!,
            _configuration["Push:VapidPrivateKey"]!);
        var webPushSubscription = new PushSubscription(
            subscription.Endpoint,
            subscription.P256Dh,
            subscription.Auth);

        using var client = new WebPushClient(_httpClient);
        await client.SendNotificationAsync(
            webPushSubscription,
            payload,
            vapidDetails,
            cancellationToken);
    }
}
