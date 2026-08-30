using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Integration;

public class OrderSubmissionApiTests : IClassFixture<WebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly WebAppFactory _factory;
    private readonly HttpClient _client;

    public OrderSubmissionApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/order")]
    [InlineData("/api/admin/orders")]
    public async Task PublicOrderEndpoints_CreateOnlyServerOwnedState(string route)
    {
        var customerName = $"  Contract {Guid.NewGuid():N}  ";
        var before = DateTime.UtcNow;
        var response = await _client.PostAsJsonAsync(route, new
        {
            customerName,
            orderItems = new[]
            {
                new
                {
                    menuItemId = 1,
                    quantity = 1,
                    notes = "  Light ice  ",
                    addOns = Array.Empty<object>()
                }
            }
        });
        var after = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublicOrderDto>(JsonOptions);
        Assert.NotNull(created);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Orders
            .Include(order => order.OrderItems)
            .SingleAsync(order => order.Id == created!.Id);

        Assert.Equal(customerName.Trim(), stored.CustomerName);
        Assert.Null(stored.CustomerPhone);
        Assert.Null(stored.CustomerEmail);
        Assert.False(stored.CustomerNotificationOptIn);
        Assert.InRange(stored.OrderDate, before, after);
        Assert.Equal(OrderStatus.Received, stored.OrderStatus);
        Assert.Null(stored.PaidUtc);
        Assert.Null(stored.PaymentProvider);
        Assert.Null(stored.PaymentReference);
        Assert.Equal(43, stored.TrackingToken.Length);
        var line = Assert.Single(stored.OrderItems);
        Assert.Equal("Light ice", line.Notes);
        Assert.Equal(4m, line.UnitPrice);
        Assert.Equal("Integration test coffee", line.ItemName);
    }

    [Theory]
    [InlineData("/api/order")]
    [InlineData("/api/admin/orders")]
    public async Task PublicOrderEndpoints_RejectForgedAndContactFields(string route)
    {
        var json = $$"""
        {
          "id": 987,
          "trackingToken": "forged",
          "customerName": "Forged {{Guid.NewGuid():N}}",
          "customerPhone": "5551234567",
          "customerEmail": "customer@example.com",
          "customerNotificationOptIn": true,
          "orderDate": "2000-01-01T00:00:00Z",
          "orderStatus": 3,
          "paidUtc": "2000-01-01T00:00:00Z",
          "paymentProvider": "forged",
          "paymentReference": "forged",
          "orderItems": [{ "menuItemId": 1, "quantity": 1 }]
        }
        """;

        await AssertStructuredBadRequestAsync(route, json);
    }

    [Theory]
    [InlineData("\"id\": 12,")]
    [InlineData("\"orderId\": 12,")]
    [InlineData("\"unitPrice\": 0.01,")]
    [InlineData("\"itemName\": \"Forged\",")]
    [InlineData("\"itemCategoryType\": 2,")]
    public async Task OrderItemServerFields_AreRejected(string forgedProperty)
    {
        var json = $$"""
        {
          "customerName": "Nested forged {{Guid.NewGuid():N}}",
          "orderItems": [{ {{forgedProperty}} "menuItemId": 1, "quantity": 1 }]
        }
        """;

        await AssertStructuredBadRequestAsync("/api/order", json);
    }

    [Theory]
    [InlineData("\"id\": 12,")]
    [InlineData("\"orderItemId\": 12,")]
    [InlineData("\"unitPrice\": 0.01,")]
    [InlineData("\"itemName\": \"Forged\",")]
    public async Task AddOnServerFields_AreRejected(string forgedProperty)
    {
        var flavorId = await AddMenuItemAsync(CategoryType.FLAVORS, 0.50m);
        var json = $$"""
        {
          "customerName": "Add-on forged {{Guid.NewGuid():N}}",
          "orderItems": [{
            "menuItemId": 1,
            "quantity": 1,
            "addOns": [{ {{forgedProperty}} "menuItemId": {{flavorId}}, "quantity": 1 }]
          }]
        }
        """;

        await AssertStructuredBadRequestAsync("/api/order", json);
    }

    [Theory]
    [InlineData("{ \"customerName\": \"Null line\", \"orderItems\": [null] }")]
    [InlineData("{ \"customerName\": \"Null add-on\", \"orderItems\": [{ \"menuItemId\": 1, \"quantity\": 1, \"addOns\": [null] }] }")]
    public async Task NullNestedEntries_AreRejected(string json)
    {
        await AssertStructuredBadRequestAsync("/api/order", json);
    }

    [Fact]
    public async Task ExactSizeBoundaries_AreAccepted()
    {
        var drinkId = await AddMenuItemAsync(CategoryType.DRINKS, 0.01m);
        var flavorIds = new List<int>();
        for (var index = 0; index < CreateOrderItemRequest.MaxDistinctFlavors; index++)
        {
            flavorIds.Add(await AddMenuItemAsync(CategoryType.FLAVORS, 0.01m));
        }

        var items = Enumerable.Range(0, CreateOrderRequest.MaxPrimaryLines)
            .Select(index => new CreateOrderItemRequest
            {
                MenuItemId = drinkId,
                Quantity = index switch { 0 or 1 => 12, 2 => 9, _ => 1 },
                Notes = index == 0 ? new string('n', CreateOrderItemRequest.MaxNotesLength) : null,
                AddOns = index == 0
                    ? flavorIds.Select(id => new CreateOrderAddOnRequest
                    {
                        MenuItemId = id,
                        Quantity = CreateOrderItemRequest.MaxQuantity
                    }).ToList()
                    : []
            })
            .ToList();
        var request = new CreateOrderRequest
        {
            CustomerName = new string('c', CreateOrderRequest.MaxCustomerNameLength),
            OrderItems = items
        };

        var response = await _client.PostAsJsonAsync("/api/order", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task PrimaryQuantity_OutsideOneThroughTwelve_IsRejected(int quantity)
    {
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Quantity {quantity} {Guid.NewGuid():N}",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = 1, Quantity = quantity }]
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task AddOnQuantity_OutsideOneThroughTwelve_IsRejected(int quantity)
    {
        var flavorId = await AddMenuItemAsync(CategoryType.FLAVORS, 0.50m);
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Add-on quantity {quantity} {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest
                {
                    MenuItemId = 1,
                    Quantity = 1,
                    AddOns = [new CreateOrderAddOnRequest { MenuItemId = flavorId, Quantity = quantity }]
                }
            ]
        });
    }

    [Fact]
    public async Task WhitespaceCustomerName_IsRejected()
    {
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = "   ",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = 1, Quantity = 1 }]
        });
    }

    [Fact]
    public async Task CustomerNameOverOneHundredCharacters_IsRejected()
    {
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = new string('c', CreateOrderRequest.MaxCustomerNameLength + 1),
            OrderItems = [new CreateOrderItemRequest { MenuItemId = 1, Quantity = 1 }]
        });
    }

    [Fact]
    public async Task TwentyOnePrimaryLines_AreRejected()
    {
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Too many lines {Guid.NewGuid():N}",
            OrderItems = Enumerable.Range(0, CreateOrderRequest.MaxPrimaryLines + 1)
                .Select(_ => new CreateOrderItemRequest { MenuItemId = 1, Quantity = 1 })
                .ToList()
        });
    }

    [Fact]
    public async Task FiftyOneDrinkUnits_AreRejected()
    {
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Too many units {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 12 },
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 12 },
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 12 },
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 12 },
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 3 }
            ]
        });
    }

    [Fact]
    public async Task NotesOverFiveHundredCharacters_AreRejected()
    {
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Long notes {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest
                {
                    MenuItemId = 1,
                    Quantity = 1,
                    Notes = new string('n', CreateOrderItemRequest.MaxNotesLength + 1)
                }
            ]
        });
    }

    [Fact]
    public async Task ThirteenDistinctFlavors_AreRejected()
    {
        var addOns = new List<CreateOrderAddOnRequest>();
        for (var index = 0; index < CreateOrderItemRequest.MaxDistinctFlavors + 1; index++)
        {
            addOns.Add(new CreateOrderAddOnRequest
            {
                MenuItemId = await AddMenuItemAsync(CategoryType.FLAVORS, 0.01m),
                Quantity = 1
            });
        }

        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Too many flavors {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest { MenuItemId = 1, Quantity = 1, AddOns = addOns }
            ]
        });
    }

    [Fact]
    public async Task DuplicateFlavorOnOneDrink_IsRejected()
    {
        var flavorId = await AddMenuItemAsync(CategoryType.FLAVORS, 0.50m);
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Duplicate flavor {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest
                {
                    MenuItemId = 1,
                    Quantity = 1,
                    AddOns =
                    [
                        new CreateOrderAddOnRequest { MenuItemId = flavorId, Quantity = 1 },
                        new CreateOrderAddOnRequest { MenuItemId = flavorId, Quantity = 1 }
                    ]
                }
            ]
        });
    }

    [Fact]
    public async Task UnknownAndArchivedMenuItems_AreRejectedWithoutSaving()
    {
        var archivedId = await AddMenuItemAsync(CategoryType.DRINKS, 4m, isArchived: true);
        var customerName = $"Unavailable {Guid.NewGuid():N}";
        var request = new CreateOrderRequest
        {
            CustomerName = customerName,
            OrderItems =
            [
                new CreateOrderItemRequest { MenuItemId = archivedId, Quantity = 1 },
                new CreateOrderItemRequest { MenuItemId = int.MaxValue, Quantity = 1 }
            ]
        };

        await AssertStructuredBadRequestAsync(request);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Orders.AnyAsync(order => order.CustomerName == customerName));
    }

    [Fact]
    public async Task PrimaryAndAddOnCategoryRules_AreEnforced()
    {
        var flavorId = await AddMenuItemAsync(CategoryType.FLAVORS, 0.50m);
        var drinkId = await AddMenuItemAsync(CategoryType.DRINKS, 4m);
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Category rules {Guid.NewGuid():N}",
            OrderItems =
            [
                new CreateOrderItemRequest
                {
                    MenuItemId = flavorId,
                    Quantity = 1,
                    AddOns = [new CreateOrderAddOnRequest { MenuItemId = drinkId, Quantity = 1 }]
                }
            ]
        });
    }

    [Fact]
    public async Task UndefinedMenuCategory_IsRejected()
    {
        var invalidCategoryId = await AddMenuItemAsync((CategoryType)999, 4m);
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Undefined category {Guid.NewGuid():N}",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = invalidCategoryId, Quantity = 1 }]
        });
    }

    [Fact]
    public async Task FiveHundredDollarOrder_IsAccepted_AndHigherOrderIsRejected()
    {
        var exactId = await AddMenuItemAsync(CategoryType.SPECIALS, 500m);
        var overId = await AddMenuItemAsync(CategoryType.SPECIALS, 500.01m);
        var accepted = await _client.PostAsJsonAsync("/api/order", new CreateOrderRequest
        {
            CustomerName = $"Exact value {Guid.NewGuid():N}",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = exactId, Quantity = 1 }]
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        await AssertStructuredBadRequestAsync(new CreateOrderRequest
        {
            CustomerName = $"Over value {Guid.NewGuid():N}",
            OrderItems = [new CreateOrderItemRequest { MenuItemId = overId, Quantity = 1 }]
        });
    }

    private async Task<int> AddMenuItemAsync(
        CategoryType category,
        decimal price,
        bool isArchived = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new MenuItem
        {
            Name = $"Contract item {Guid.NewGuid():N}",
            Description = "Order contract integration test",
            Price = price,
            CategoryType = category,
            IsArchived = isArchived
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task AssertStructuredBadRequestAsync(CreateOrderRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/order", request, JsonOptions);
        await AssertStructuredBadRequestAsync(response);
    }

    private async Task AssertStructuredBadRequestAsync(string route, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(route, content);
        await AssertStructuredBadRequestAsync(response);
    }

    private static async Task AssertStructuredBadRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        Assert.NotEmpty(errors.EnumerateObject());
    }
}
