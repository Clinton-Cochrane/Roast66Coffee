using System.Net;
using System.Text.Json;
using CoffeeShopApi.Controllers;
using CoffeeShopApi.Data;
using CoffeeShopApi.Middleware;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using CoffeeShopApi.Services.Payments;
using CoffeeShopApi.Services.Sms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeShopApi.Tests;

public class SensitiveLoggingTests
{
    private const string TrackingToken = "tracking-secret-1234567890123456789012345678";
    private const string Jwt = "eyJhbGciOiJIUzI1NiJ9.sensitive-payload.signature";
    private const string ConnectionString = "Host=db;Username=admin;Password=connection-secret";
    private const string ProviderSecret = "provider-secret-response-body";

    [Fact]
    public async Task RequestLog_UsesRouteTemplateWithoutPathOrQuerySecrets()
    {
        var logger = new RecordingLogger<SafeRequestLoggingMiddleware>();
        var middleware = new SafeRequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            },
            logger);
        var context = CreateTrackingContext();

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal("GET", entry.Properties["RequestMethod"]);
        Assert.Equal("api/order/track/{trackingToken}", entry.Properties["RouteTemplate"]);
        Assert.Equal(StatusCodes.Status404NotFound, entry.Properties["StatusCode"]);
        Assert.Equal("trace-safe-123", entry.Properties["TraceId"]);
        Assert.True(Convert.ToDouble(entry.Properties["ElapsedMilliseconds"]) >= 0);
        AssertSensitiveValuesAbsent(entry.Message);
    }

    [Fact]
    public async Task RequestLog_RecordsSafeFailureTypeWithoutExceptionMessage()
    {
        var logger = new RecordingLogger<SafeRequestLoggingMiddleware>();
        var middleware = new SafeRequestLoggingMiddleware(
            _ => throw new InvalidOperationException(
                $"Request failed with {TrackingToken}, {Jwt}, {ConnectionString}, and {ProviderSecret}."),
            logger);
        var context = CreateTrackingContext();
        context.Request.Method = $"CUSTOM\r\n{ProviderSecret}";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal("OTHER", entry.Properties["RequestMethod"]);
        Assert.Equal(nameof(InvalidOperationException), entry.Properties["FailureType"]);
        Assert.Equal(StatusCodes.Status500InternalServerError, entry.Properties["StatusCode"]);
        AssertSensitiveValuesAbsent(entry.Message);
    }

    [Fact]
    public async Task RequestLog_UsesSafeFallbackForUnrecognizedMethod()
    {
        var logger = new RecordingLogger<SafeRequestLoggingMiddleware>();
        var middleware = new SafeRequestLoggingMiddleware(_ => Task.CompletedTask, logger);
        var context = CreateTrackingContext();
        context.Request.Method = $"CUSTOM\r\n{ProviderSecret}";

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("OTHER", entry.Properties["RequestMethod"]);
        AssertSensitiveValuesAbsent(entry.Message);
    }

    [Fact]
    public async Task PaymentWebhookLog_OmitsRouteProvidedProvider()
    {
        await using var context = CreateContext();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Payments:DefaultProvider"] = RejectingPaymentGateway.Name
        });
        var paymentService = new PaymentService(
            context,
            configuration,
            new OrderService(context, configuration),
            [new RejectingPaymentGateway()],
            new RecordingLogger<PaymentService>());
        var logger = new RecordingLogger<PaymentsController>();
        var controller = new PaymentsController(paymentService, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Body = new MemoryStream("{}"u8.ToArray());

        var result = await controller.HandleProviderWebhook(
            RejectingPaymentGateway.Name,
            default);

        Assert.IsType<BadRequestResult>(result);
        var entry = Assert.Single(logger.Entries);
        Assert.False(entry.Properties.ContainsKey("Provider"));
        Assert.DoesNotContain(RejectingPaymentGateway.Name, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailFailureLogs_StatusWithoutProviderResponseBody()
    {
        var responseBody = $"{ProviderSecret}; token={Jwt}; connection={ConnectionString}";
        var factory = new StubHttpClientFactory(
            new HttpClient(new StubHttpHandler(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(responseBody)
                })));
        var logger = new RecordingLogger<OrderEmailNotificationService>();
        var service = new OrderEmailNotificationService(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = ProviderSecret,
                ["Resend:From"] = "orders@example.test"
            }),
            factory,
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendReadyForPickupAsync(new Order
            {
                Id = 66,
                CustomerName = "Sensitive Customer",
                CustomerEmail = "customer@example.test",
                OrderItems = []
            }));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("BadRequest", entry.Message);
        AssertSensitiveValuesAbsent(entry.Message);
        Assert.DoesNotContain("customer@example.test", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupportEmailFailureLogs_StatusWithoutProviderResponseBody()
    {
        var responseBody = $"{ProviderSecret}; token={Jwt}; connection={ConnectionString}";
        var factory = new StubHttpClientFactory(
            new HttpClient(new StubHttpHandler(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(responseBody)
                })));
        var logger = new RecordingLogger<SupportEmailService>();
        var service = new SupportEmailService(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = ProviderSecret,
                ["Resend:From"] = "support@example.test",
                ["Support:AlertEmail"] = "owner@example.test"
            }),
            factory,
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendForgotPasswordAlertAsync("192.0.2.10", "customer requested help", default));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("Unauthorized", entry.Message);
        AssertSensitiveValuesAbsent(entry.Message);
        Assert.DoesNotContain("owner@example.test", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotificationAuditRecord_OmitsCustomerPayloadAndExceptionMessage()
    {
        await using var context = CreateContext();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Notifications:SmsEnabled"] = "true"
        });
        var emailService = new OrderEmailNotificationService(
            configuration,
            new StubHttpClientFactory(new HttpClient(new StubHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)))),
            new RecordingLogger<OrderEmailNotificationService>());
        var service = new NotificationService(
            configuration,
            context,
            new NotificationSettingsService(context),
            new FailingSmsSender($"{ProviderSecret}; {ConnectionString}"),
            emailService);
        var order = new Order
        {
            Id = 166,
            CustomerName = "Sensitive Customer",
            CustomerPhone = "555-867-5309",
            OrderStatus = OrderStatus.ReadyForPickup,
            OrderItems = []
        };

        await service.SendReadyForPickupNotificationAsync(order);

        var audit = Assert.Single(await context.NotificationMessages.ToListAsync());
        var persistedLog = JsonSerializer.Serialize(audit);
        Assert.Contains("166", audit.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(order.CustomerName, persistedLog, StringComparison.Ordinal);
        Assert.DoesNotContain(order.CustomerPhone!, persistedLog, StringComparison.Ordinal);
        Assert.DoesNotContain("5558675309", persistedLog, StringComparison.Ordinal);
        Assert.DoesNotContain(ProviderSecret, audit.LastError ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("connection-secret", audit.LastError ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), audit.LastError ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailNotification_DeliversWithoutPersistingCustomerDestination()
    {
        await using var context = CreateContext();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = "test-api-key",
            ["Resend:From"] = "orders@example.test"
        });
        var order = new Order
        {
            Id = 167,
            CustomerName = "Email Retention Customer",
            CustomerEmail = "private-customer@example.test",
            CustomerNotificationOptIn = true,
            TrackingToken = "email-retention-token-0000000000000000000000",
            OrderStatus = OrderStatus.ReadyForPickup,
            OrderItems = []
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        var service = new NotificationService(
            configuration,
            context,
            new NotificationSettingsService(context),
            new DisabledSmsSender(),
            new OrderEmailNotificationService(
                configuration,
                new StubHttpClientFactory(new HttpClient(new StubHttpHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)))),
                new RecordingLogger<OrderEmailNotificationService>()));

        await service.SendReadyForPickupNotificationAsync(order);

        var audit = Assert.Single(await context.NotificationMessages.ToListAsync());
        Assert.Equal("email", audit.Channel);
        Assert.Equal("sent", audit.Status);
        var persistedLog = JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(order.CustomerName, persistedLog, StringComparison.Ordinal);
        Assert.DoesNotContain(order.CustomerEmail!, persistedLog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderStatusAudit_OmitsProviderErrorDetails()
    {
        await using var context = CreateContext();
        var notification = CreateNotification("sms", DateTime.UtcNow);
        notification.Provider = "test-provider";
        notification.ProviderMessageId = "provider-message-166";
        context.NotificationMessages.Add(notification);
        await context.SaveChangesAsync();
        var service = new NotificationService(
            BuildConfiguration([]),
            context,
            new NotificationSettingsService(context),
            new FailingSmsSender(ProviderSecret),
            new OrderEmailNotificationService(
                BuildConfiguration([]),
                new StubHttpClientFactory(new HttpClient(new StubHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)))),
                new RecordingLogger<OrderEmailNotificationService>()));

        await service.UpdateProviderStatusAsync(
            notification.Provider,
            notification.ProviderMessageId,
            "failed",
            $"{ProviderSecret}; {ConnectionString}; {Jwt}");

        Assert.Equal("Provider reported a delivery failure.", notification.LastError);
        AssertSensitiveValuesAbsent(notification.LastError!);
    }

    [Fact]
    public async Task RetentionPurge_RemovesOldNotificationRecordsAcrossChannels()
    {
        await using var context = CreateContext();
        var oldEmail = CreateNotification("email", DateTime.UtcNow.AddDays(-91));
        var oldSms = CreateNotification("sms", DateTime.UtcNow.AddDays(-91));
        var recentSms = CreateNotification("sms", DateTime.UtcNow.AddDays(-89));
        context.NotificationMessages.AddRange(oldEmail, oldSms, recentSms);
        await context.SaveChangesAsync();
        var service = new DataRetentionService(
            context,
            Options.Create(new DataRetentionOptions()),
            TimeProvider.System);

        var result = await service.PurgeExpiredDataAsync();

        Assert.Equal(2, result.NotificationLogsDeleted);
        Assert.Null(await context.NotificationMessages.FindAsync(oldEmail.Id));
        Assert.Null(await context.NotificationMessages.FindAsync(oldSms.Id));
        Assert.NotNull(await context.NotificationMessages.FindAsync(recentSms.Id));
    }

    private static DefaultHttpContext CreateTrackingContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = $"/api/order/track/{TrackingToken}";
        context.Request.QueryString = new QueryString($"?access_token={Uri.EscapeDataString(Jwt)}");
        context.TraceIdentifier = "trace-safe-123";
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("api/order/track/{trackingToken}"),
            order: 0,
            EndpointMetadataCollection.Empty,
            "Track order"));
        return context;
    }

    private static NotificationMessage CreateNotification(string channel, DateTime createdUtc) =>
        new()
        {
            EventType = "retention.test",
            RecipientRole = "customer",
            Channel = channel,
            TemplateKey = "retention_test",
            PayloadJson = "{}",
            DedupKey = Guid.NewGuid().ToString("N"),
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc
        };

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"SensitiveLoggingTests-{Guid.NewGuid():N}")
            .Options);

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static void AssertSensitiveValuesAbsent(string logText)
    {
        Assert.DoesNotContain(TrackingToken, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(Jwt, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionString, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(ProviderSecret, logText, StringComparison.Ordinal);
    }

    private sealed class FailingSmsSender(string errorMessage) : ISmsSender
    {
        public string ProviderName => "test-provider";

        public bool IsConfigured() => true;

        public Task<SmsSendResult> SendAsync(
            SmsSendRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(errorMessage);
    }

    private sealed class RejectingPaymentGateway : IPaymentGateway
    {
        public const string Name = "route-provided-test-provider";

        public string ProviderName => Name;

        public bool IsConfigured() => true;

        public Task<GatewayCheckoutResult> CreateCheckoutAsync(
            GatewayCheckoutRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GatewayCheckoutResult> GetCheckoutAsync(
            string providerCheckoutId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GatewayPaymentEvent?> ParseWebhookAsync(
            string body,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken = default) =>
            throw new PaymentWebhookException("Rejected test webhook.");
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }
}
