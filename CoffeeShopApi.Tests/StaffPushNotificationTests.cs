using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoffeeShopApi.Tests;

public class StaffPushNotificationTests
{
    [Fact]
    public async Task SendNewOrderAlertAsync_TimesOutAndStopsAtConfiguredAttemptLimit()
    {
        await using var context = CreateContext();
        context.StaffPushSubscriptions.Add(CreateSubscription());
        await context.SaveChangesAsync();

        var sender = new RecordingSender((_, _, cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        var service = CreateService(
            context,
            sender,
            new StaffPushOptions
            {
                RequestTimeout = TimeSpan.FromMilliseconds(20),
                MaxAttempts = 2,
                RetryDelay = TimeSpan.Zero
            });

        await service.SendNewOrderAlertAsync(101).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(2, sender.Attempts.Count);
    }

    [Fact]
    public async Task SendNewOrderAlertAsync_ContinuesAfterPartialFailureAndRecoversOnRetry()
    {
        await using var context = CreateContext();
        var failingSubscription = CreateSubscription();
        var recoveringSubscription = CreateSubscription();
        context.StaffPushSubscriptions.AddRange(failingSubscription, recoveringSubscription);
        await context.SaveChangesAsync();

        var attemptsBySubscription = new Dictionary<Guid, int>();
        var sender = new RecordingSender((subscription, _, _) =>
        {
            attemptsBySubscription.TryGetValue(subscription.Id, out var attempts);
            attemptsBySubscription[subscription.Id] = ++attempts;

            if (subscription.Id == failingSubscription.Id || attempts == 1)
            {
                throw new InvalidOperationException("simulated provider failure");
            }

            return Task.CompletedTask;
        });
        var service = CreateService(
            context,
            sender,
            new StaffPushOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(1),
                MaxAttempts = 2,
                RetryDelay = TimeSpan.Zero
            });

        await service.SendNewOrderAlertAsync(202);

        Assert.Equal(2, attemptsBySubscription[failingSubscription.Id]);
        Assert.Equal(2, attemptsBySubscription[recoveringSubscription.Id]);
    }

    [Fact]
    public async Task SendNewOrderAlertAsync_LogsRepeatedFailuresWithoutSecretsOrPii()
    {
        await using var context = CreateContext();
        var subscription = CreateSubscription();
        context.StaffPushSubscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var logger = new RecordingLogger<StaffPushNotificationService>();
        var sender = new RecordingSender((_, _, _) =>
            throw new InvalidOperationException("provider response contained a secret"));
        var service = CreateService(
            context,
            sender,
            new StaffPushOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(1),
                MaxAttempts = 2,
                RetryDelay = TimeSpan.Zero
            },
            logger);

        await service.SendNewOrderAlertAsync(505);

        var logText = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("505", logText);
        Assert.Contains(subscription.Id.ToString(), logText);
        Assert.Contains(nameof(InvalidOperationException), logText);
        Assert.DoesNotContain(subscription.Endpoint, logText);
        Assert.DoesNotContain(subscription.P256Dh, logText);
        Assert.DoesNotContain(subscription.Auth, logText);
        Assert.DoesNotContain("provider response contained a secret", logText);
    }

    [Fact]
    public async Task Queue_DeduplicatesAnOrderWithinTheCurrentProcess()
    {
        var queue = CreateQueue();

        Assert.True(queue.TryEnqueue(303));
        Assert.False(queue.TryEnqueue(303));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var reader = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(303, reader.Current.OrderId);
    }

    [Fact]
    public void Queue_DeduplicationIsIntentionallyNotDurableAcrossProcessInstances()
    {
        var firstProcessQueue = CreateQueue();
        var restartedProcessQueue = CreateQueue();

        Assert.True(firstProcessQueue.TryEnqueue(404));
        Assert.True(restartedProcessQueue.TryEnqueue(404));
    }

    private static StaffPushNotificationService CreateService(
        ApplicationDbContext context,
        IStaffPushSender sender,
        StaffPushOptions options,
        ILogger<StaffPushNotificationService>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Push:Subject"] = "mailto:staff@example.com",
                ["Push:VapidPublicKey"] = "test-public-key",
                ["Push:VapidPrivateKey"] = "test-private-key"
            })
            .Build();

        return new StaffPushNotificationService(
            context,
            configuration,
            sender,
            Options.Create(options),
            logger ?? NullLogger<StaffPushNotificationService>.Instance);
    }

    private static StaffPushNotificationQueue CreateQueue() =>
        new(
            Options.Create(new StaffPushOptions()),
            NullLogger<StaffPushNotificationQueue>.Instance);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"StaffPushTests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static StaffPushSubscription CreateSubscription() =>
        new()
        {
            Endpoint = $"https://push.example.com/{Guid.NewGuid():N}",
            P256Dh = "test-p256dh",
            Auth = "test-auth"
        };

    private sealed class RecordingSender(
        Func<StaffPushSubscription, string, CancellationToken, Task> send) : IStaffPushSender
    {
        public List<Guid> Attempts { get; } = [];

        public Task SendAsync(
            StaffPushSubscription subscription,
            string payload,
            CancellationToken cancellationToken)
        {
            Attempts.Add(subscription.Id);
            return send(subscription, payload, cancellationToken);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
