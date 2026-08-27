using CoffeeShopApi.Services.Sms;

namespace CoffeeShopApi.Tests;

public class SmsSenderTests
{
    [Fact]
    public void DisabledSender_IsExplicitlyUnavailable()
    {
        var sender = new DisabledSmsSender();

        Assert.Equal("disabled", sender.ProviderName);
        Assert.False(sender.IsConfigured());
    }

    [Fact]
    public async Task DisabledSender_RejectsSendAttempts()
    {
        var sender = new DisabledSmsSender();
        var request = new SmsSendRequest("+15551234567", "Test message");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(request));

        Assert.Equal("No SMS provider is configured.", exception.Message);
    }
}
