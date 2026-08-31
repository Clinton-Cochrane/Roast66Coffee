using System.Net.Http.Json;
using System.Text.Json;

namespace CoffeeShopApi.Tests.Integration;

internal static class OrderRequestExtensions
{
    public static Task<HttpResponseMessage> PostOrderAsync<T>(
        this HttpClient client,
        T order,
        string? idempotencyKey = null,
        JsonSerializerOptions? options = null,
        string path = "/api/order")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(order, options: options)
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }
}
