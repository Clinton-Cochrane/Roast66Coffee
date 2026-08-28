namespace CoffeeShopApi.Services.Sms;

public sealed class DisabledSmsSender : ISmsSender
{
    public string ProviderName => "disabled";

    public bool IsConfigured() => false;

    public Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No SMS provider is configured.");
}
