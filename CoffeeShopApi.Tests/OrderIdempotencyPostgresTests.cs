using System.Net;
using System.Net.Http.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoffeeShopApi.Tests;

[Collection(PostgresIntegrationCollection.Name)]
public class OrderIdempotencyPostgresTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentHttpRetries_CreateOneDurableOrderAndEnqueueOneNotification()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("roast66_order_idempotency");
        if (database == null)
        {
            return;
        }

        int menuItemId;
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            var menuItem = new MenuItem
            {
                Name = "Concurrent Latte",
                Description = "PostgreSQL race test",
                Price = 5m,
                CategoryType = CategoryType.COFFEE
            };
            context.MenuItems.Add(menuItem);
            await context.SaveChangesAsync();
            menuItemId = menuItem.Id;
        }

        await using var factory = new PostgresOrderWebAppFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 8)
            .Select(async _ =>
            {
                await start.Task;
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/order")
                {
                    Content = JsonContent.Create(CreateOrder(menuItemId))
                };
                request.Headers.Add("X-Idempotency-Key", idempotencyKey);
                return await client.SendAsync(request);
            })
            .ToArray();

        start.SetResult();
        var responses = await Task.WhenAll(requests);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        var ids = new List<int>();
        foreach (var response in responses)
        {
            var order = await response.Content.ReadFromJsonAsync<PublicOrderDto>();
            ids.Add(order!.Id);
        }
        Assert.Single(ids.Distinct());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await context.Orders.CountAsync(o => o.IdempotencyKey == idempotencyKey));
            Assert.Equal(1, await context.OrderItems.CountAsync(item => item.OrderId == ids[0]));
        }
        Assert.Equal(1, factory.NotificationQueue.EnqueueCount);
    }

    private static Order CreateOrder(int menuItemId) =>
        new()
        {
            CustomerName = "Concurrent Customer",
            CustomerPhone = "5551234567",
            OrderItems = [new OrderItem { MenuItemId = menuItemId, Quantity = 1 }]
        };

    private sealed class PostgresOrderWebAppFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        public CountingNotificationQueue NotificationQueue { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AllowedOrigins"] = "http://localhost",
                    ["Jwt:Key"] = "IntegrationTestSigningKey_NotForProduction_Min32Chars___",
                    ["Jwt:Issuer"] = "Roast66Coffee",
                    ["Jwt:Audience"] = "Roast66Coffee"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
                services.RemoveAll<IStaffPushNotificationQueue>();
                services.AddSingleton<IStaffPushNotificationQueue>(NotificationQueue);
            });
        }
    }

    internal sealed class CountingNotificationQueue : IStaffPushNotificationQueue
    {
        private int _enqueueCount;
        public int EnqueueCount => Volatile.Read(ref _enqueueCount);

        public bool TryEnqueue(int orderId)
        {
            Interlocked.Increment(ref _enqueueCount);
            return true;
        }
    }
}
