using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CoffeeShopApi.Tests.Integration;

public class StaffAuthenticationApiTests : IClassFixture<WebAppFactory>
{
    private const string OwnerUsername = "integration-owner";
    private const string AdminUsername = "integration-admin";
    private const string Password = "IntegrationPassword1!";
    private readonly WebAppFactory _factory;

    public StaffAuthenticationApiTests(WebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NamedOwnerLogin_ReturnsIdentifiedRoleToken()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(client, OwnerUsername);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub);
        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Name && claim.Value == "Integration Owner");
        Assert.Contains(jwt.Claims, claim => claim.Type == "username" && claim.Value == OwnerUsername);
        Assert.Contains(jwt.Claims, claim => claim.Type == "role" && claim.Value == "Admin");
        Assert.Contains(jwt.Claims, claim => claim.Type == "role" && claim.Value == "Owner");
        Assert.Contains(jwt.Claims, claim => claim.Type == "security_stamp" && !string.IsNullOrWhiteSpace(claim.Value));
    }

    [Fact]
    public async Task AdminCannotManageStaff_ButOwnerCan()
    {
        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(adminClient, AdminUsername));
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/admin/staff")).StatusCode);

        using var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(ownerClient, OwnerUsername));
        var response = await ownerClient.GetAsync("/api/admin/staff");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DisablingOneUserRevokesTheirTokenWithoutRevokingOwner()
    {
        using var ownerClient = _factory.CreateClient();
        var ownerToken = await LoginAsync(ownerClient, OwnerUsername);
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var username = $"revoked-{Guid.NewGuid():N}";
        var create = await ownerClient.PostAsJsonAsync("/api/admin/staff", new
        {
            displayName = "Revoked Integration User",
            username,
            initialPassword = Password,
            isOwner = false
        });
        create.EnsureSuccessStatusCode();

        using var adminClient = _factory.CreateClient();
        var adminToken = await LoginAsync(adminClient, username);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var me = await adminClient.GetFromJsonAsync<CurrentStaffResponse>("/api/admin/me");
        Assert.NotNull(me);

        var disable = await ownerClient.PostAsync($"/api/admin/staff/{me!.Id}/disable", null);
        disable.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await adminClient.GetAsync("/api/admin/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync("/api/admin/me")).StatusCode);
    }

    [Fact]
    public async Task OwnerPasswordResetRevokesOnlyTheTargetAndAllowsTheNewPassword()
    {
        using var ownerClient = await AuthenticatedOwnerClientAsync();
        var username = $"reset-{Guid.NewGuid():N}";
        var userId = await CreateAdminAsync(ownerClient, username);

        using var targetClient = _factory.CreateClient();
        var oldToken = await LoginAsync(targetClient, username);
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);

        const string newPassword = "ChangedPassword2!";
        var reset = await ownerClient.PostAsJsonAsync($"/api/admin/staff/{userId}/reset-password", new
        {
            newPassword
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/admin/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync("/api/admin/me")).StatusCode);
        await LoginAsync(_factory.CreateClient(), username, newPassword);
    }

    [Fact]
    public async Task StaffCanChangeOwnPasswordAndTheirPreviousTokenIsRevoked()
    {
        using var ownerClient = await AuthenticatedOwnerClientAsync();
        var username = $"change-{Guid.NewGuid():N}";
        await CreateAdminAsync(ownerClient, username);

        using var targetClient = _factory.CreateClient();
        var oldToken = await LoginAsync(targetClient, username);
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        const string newPassword = "ChangedPassword3!";

        var changed = await targetClient.PostAsJsonAsync("/api/admin/me/change-password", new
        {
            currentPassword = Password,
            newPassword
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/admin/me")).StatusCode);
        await LoginAsync(_factory.CreateClient(), username, newPassword);
    }

    [Fact]
    public async Task OwnerCannotDisableTheirCurrentAccount()
    {
        using var ownerClient = await AuthenticatedOwnerClientAsync();
        var owner = await ownerClient.GetFromJsonAsync<CurrentStaffResponse>("/api/admin/me");

        var response = await ownerClient.PostAsync($"/api/admin/staff/{owner!.Id}/disable", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync("/api/admin/me")).StatusCode);
    }

    private async Task<HttpClient> AuthenticatedOwnerClientAsync()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(client, OwnerUsername));
        return client;
    }

    private static async Task<string> CreateAdminAsync(HttpClient ownerClient, string username)
    {
        var create = await ownerClient.PostAsJsonAsync("/api/admin/staff", new
        {
            displayName = "Integration Staff",
            username,
            initialPassword = Password,
            isOwner = false
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CurrentStaffResponse>();
        return created!.Id;
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string username,
        string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/admin/login", new
        {
            username,
            password
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return login!.Token;
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    private sealed class CurrentStaffResponse
    {
        public string Id { get; set; } = string.Empty;
    }
}
