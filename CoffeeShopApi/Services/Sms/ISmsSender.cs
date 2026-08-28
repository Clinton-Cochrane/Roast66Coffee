namespace CoffeeShopApi.Services.Sms;

public interface ISmsSender
{
    string ProviderName { get; }

    bool IsConfigured();

    Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record SmsSendRequest(
    string To,
    string Body,
    string? FromAddress = null);

public sealed record SmsSendResult(string ProviderMessageId);
