using System.Net;
using CoffeeShopApi.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace CoffeeShopApi.Tests.Integration;

public class CorsIntegrationTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public CorsIntegrationTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_FromAllowedOrigin_ReturnsCorsHeaders()
    {
        var loggerProvider = new RequestLogProvider();
        using var application = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider)));
        using var client = application.CreateClient();
        using var request = CreatePreflightRequest("http://localhost");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Empty(loggerProvider.Messages);
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_DoesNotReturnCorsHeaders()
    {
        using var request = CreatePreflightRequest("https://not-allowed.example");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/menu");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    private sealed class RequestLogProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(SafeRequestLoggingMiddleware).FullName
                ? new RequestLogger(Messages)
                : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }
    }

    private sealed class RequestLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}
