using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services;

public class ConnectionWarmupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KeepAliveStateStore _stateStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConnectionWarmupService> _logger;
    private readonly IConfiguration _configuration;

    public ConnectionWarmupService(
        IServiceScopeFactory scopeFactory,
        KeepAliveStateStore stateStore,
        IHttpClientFactory httpClientFactory,
        ILogger<ConnectionWarmupService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _stateStore = stateStore;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var enabled = _configuration.GetValue("KeepAlive:Enabled", false);
            var intervalMinutes = Math.Max(1, _configuration.GetValue("KeepAlive:ProbeIntervalMinutes", 4));
            var activeWindowMinutes = Math.Max(1, _configuration.GetValue("KeepAlive:ActiveWindowMinutes", 10));

            if (enabled)
            {
                await ProbeIfActiveAsync(activeWindowMinutes, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task ProbeIfActiveAsync(int activeWindowMinutes, CancellationToken cancellationToken)
    {
        var (lastHeartbeatUtc, source) = _stateStore.Snapshot();
        var isActiveWindow = lastHeartbeatUtc > DateTime.MinValue &&
            DateTime.UtcNow - lastHeartbeatUtc <= TimeSpan.FromMinutes(activeWindowMinutes);

        if (!isActiveWindow)
        {
            return;
        }

        await ProbeDatabaseAsync(cancellationToken);
        await ProbeSupabaseAsync(cancellationToken);
        _logger.LogInformation("KeepAlive probe succeeded for active source {Source}.", source);
    }

    private async Task ProbeDatabaseAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
    }

    private async Task ProbeSupabaseAsync(CancellationToken cancellationToken)
    {
        var supabaseUrl = _configuration["KeepAlive:SupabaseHeartbeatUrl"];
        if (string.IsNullOrWhiteSpace(supabaseUrl))
        {
            return;
        }

        var key = _configuration["KeepAlive:SupabaseServiceRoleKey"];
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, supabaseUrl);
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.Headers.Add("apikey", key);
            request.Headers.Add("Authorization", $"Bearer {key}");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
