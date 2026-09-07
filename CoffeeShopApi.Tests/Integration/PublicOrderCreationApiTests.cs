using System.Net;
using System.Net.Http.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Integration;

public class PublicOrderCreationApiTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public PublicOrderCreationApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostOrder_FrontendShapedRequest_ReturnsPublicOrder()
    {
        var response = await _client.PostOrderAsync(CreateRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublicOrderDto>();
        Assert.NotNull(created);
        Assert.Equal("Public contract customer", created!.CustomerName);
        Assert.Single(created.OrderItems);
    }

    [Fact]
    public async Task PostOrder_IgnoresForgedServerOwnedProperties()
    {
        var before = DateTime.UtcNow;
        var request = new
        {
            customerName = "Forged fields customer",
            customerPhone = "5551234567",
            customerEmail = "customer@example.test",
            customerNotificationOptIn = true,
            id = 999,
            trackingToken = "caller-controlled-tracking-token",
            idempotencyKey = "caller-controlled-idempotency-key",
            requestFingerprint = "caller-controlled-fingerprint",
            orderStatus = OrderStatus.Completed,
            paidUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            paymentProvider = "forged-provider",
            paymentReference = "forged-reference",
            completedUtc = new DateTime(2001, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            orderDate = new DateTime(2001, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            statusConcurrencyToken = Guid.Empty,
            orderItems = new[]
            {
                new
                {
                    id = 998,
                    orderId = 999,
                    menuItemId = 1,
                    quantity = 1,
                    notes = "No foam",
                    unitPrice = 0.01m,
                    itemName = "Forged menu name",
                    itemDescription = "Forged menu description",
                    itemCategoryType = 99,
                    addOns = new[]
                    {
                        new
                        {
                            id = 997,
                            orderItemId = 998,
                            menuItemId = 1,
                            quantity = 1,
                            unitPrice = 0.01m,
                            itemName = "Forged add-on name",
                            itemDescription = "Forged add-on description",
                            itemCategoryType = 99
                        }
                    }
                }
            }
        };

        var response = await _client.PostOrderAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublicOrderDto>();
        Assert.NotNull(created);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await context.Orders
            .Include(order => order.OrderItems)
            .ThenInclude(item => item.AddOns)
            .SingleAsync(order => order.Id == created!.Id);
        var line = Assert.Single(persisted.OrderItems);
        var addOn = Assert.Single(line.AddOns!);

        Assert.NotEqual(999, persisted.Id);
        Assert.Equal(OrderStatus.Received, persisted.OrderStatus);
        Assert.Null(persisted.PaidUtc);
        Assert.Null(persisted.PaymentProvider);
        Assert.Null(persisted.PaymentReference);
        Assert.Null(persisted.CompletedUtc);
        Assert.InRange(persisted.OrderDate, before, DateTime.UtcNow);
        Assert.Equal(43, persisted.TrackingToken.Length);
        Assert.NotEqual(Guid.Empty, persisted.StatusConcurrencyToken);
        Assert.NotEqual("caller-controlled-idempotency-key", persisted.IdempotencyKey);
        Assert.NotEqual("caller-controlled-fingerprint", persisted.RequestFingerprint);
        Assert.NotEqual(998, line.Id);
        Assert.Equal("Integration test coffee", line.ItemName);
        Assert.Equal(4m, line.UnitPrice);
        Assert.NotEqual(997, addOn.Id);
        Assert.Equal("Integration test coffee", addOn.ItemName);
        Assert.Equal(4m, addOn.UnitPrice);
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(1, HttpStatusCode.Created)]
    [InlineData(12, HttpStatusCode.Created)]
    [InlineData(13, HttpStatusCode.BadRequest)]
    public async Task PostOrder_EnforcesPrimaryQuantityBounds(int quantity, HttpStatusCode expectedStatus)
    {
        var before = await GetGraphCountsAsync();

        var response = await _client.PostOrderAsync(CreateRequest(quantity));

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.BadRequest)
        {
            Assert.Equal(before, await GetGraphCountsAsync());
        }
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.BadRequest)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(1, HttpStatusCode.Created)]
    [InlineData(12, HttpStatusCode.Created)]
    [InlineData(13, HttpStatusCode.BadRequest)]
    public async Task PostOrder_EnforcesAddOnQuantityBounds(int quantity, HttpStatusCode expectedStatus)
    {
        var before = await GetGraphCountsAsync();

        var response = await _client.PostOrderAsync(CreateRequest(addOnQuantity: quantity));

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.BadRequest)
        {
            Assert.Equal(before, await GetGraphCountsAsync());
        }
    }

    private static object CreateRequest(int quantity = 1, int? addOnQuantity = null) => new
    {
        customerName = "Public contract customer",
        customerPhone = "5551234567",
        customerEmail = "customer@example.test",
        customerNotificationOptIn = true,
        orderItems = new[]
        {
            new
            {
                menuItemId = 1,
                quantity,
                notes = "Light ice",
                addOns = addOnQuantity.HasValue
                    ? new[] { new { menuItemId = 1, quantity = addOnQuantity.Value } }
                    : []
            }
        }
    };

    private async Task<OrderGraphCounts> GetGraphCountsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new OrderGraphCounts(
            await context.Orders.CountAsync(),
            await context.OrderItems.CountAsync(),
            await context.Set<AddOn>().CountAsync());
    }

    private sealed record OrderGraphCounts(int Orders, int OrderItems, int AddOns);
}
