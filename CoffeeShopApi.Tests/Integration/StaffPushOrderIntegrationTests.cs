using System.Net;
using System.Net.Http.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoffeeShopApi.Tests.Integration;

public class StaffPushOrderIntegrationTests
{
    [Fact]
    public async Task PostOrder_ReturnsWhilePushProviderIsStillBlocked()
    {
        var sender = new BlockingPushSender();
        await using var factory = new PushWebAppFactory(sender);
        using var client = factory.CreateClient();
        await AddSubscriptionAsync(factory.Services);

        var postTask = client.PostOrderAsync(CreateValidOrder());

        await sender.Started.WaitAsync(TimeSpan.FromSeconds(2));
        var completed = await Task.WhenAny(postTask, Task.Delay(TimeSpan.FromMilliseconds(500)));

        try
        {
            Assert.Same(postTask, completed);
            var response = await postTask;
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        finally
        {
            sender.Release();
        }
    }

    [Fact]
    public async Task PostOrder_RemainsTrackableAfterPushProviderFailure()
    {
        var sender = new FailingPushSender();
        await using var factory = new PushWebAppFactory(sender);
        using var client = factory.CreateClient();
        await AddSubscriptionAsync(factory.Services);

        var response = await client.PostOrderAsync(CreateValidOrder());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublicOrderDto>();
        Assert.NotNull(created);

        await sender.Attempted.WaitAsync(TimeSpan.FromSeconds(2));
        var trackingResponse = await client.GetAsync($"/api/order/track/{created!.TrackingToken}");

        Assert.Equal(HttpStatusCode.OK, trackingResponse.StatusCode);
        var tracked = await trackingResponse.Content.ReadFromJsonAsync<PublicOrderDto>();
        Assert.Equal(created.Id, tracked!.Id);
    }

    private static async Task AddSubscriptionAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.StaffPushSubscriptions.Add(new StaffPushSubscription
        {
            Endpoint = $"https://push.example.com/{Guid.NewGuid():N}",
            P256Dh = "test-p256dh",
            Auth = "test-auth"
        });
        await context.SaveChangesAsync();
    }

    private static Order CreateValidOrder() =>
        new()
        {
            CustomerName = $"Push Test {Guid.NewGuid():N}",
            CustomerPhone = $"555{Random.Shared.Next(1000000, 9999999)}",
            OrderItems = [new OrderItem { MenuItemId = 1, Quantity = 1 }]
        };

    private sealed class PushWebAppFactory(IStaffPushSender sender) : WebAppFactory
    {
        private readonly string _databaseName = $"StaffPushOrderTests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Push:Subject"] = "mailto:staff@example.com",
                    ["Push:VapidPublicKey"] = "test-public-key",
                    ["Push:VapidPrivateKey"] = "test-private-key",
                    ["Push:RequestTimeout"] = "00:00:01",
                    ["Push:RetryDelay"] = "00:00:00.001"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<IStaffPushSender>();
                services.AddSingleton(sender);
            });
        }
    }

    private sealed class BlockingPushSender : IStaffPushSender
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public Task SendAsync(
            StaffPushSubscription subscription,
            string payload,
            CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            return _release.Task.WaitAsync(cancellationToken);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class FailingPushSender : IStaffPushSender
    {
        private readonly TaskCompletionSource _attempted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Attempted => _attempted.Task;

        public Task SendAsync(
            StaffPushSubscription subscription,
            string payload,
            CancellationToken cancellationToken)
        {
            _attempted.TrySetResult();
            throw new HttpRequestException("simulated provider outage");
        }
    }
}
