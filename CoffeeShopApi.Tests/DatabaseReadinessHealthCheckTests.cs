using CoffeeShopApi.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoffeeShopApi.Tests;

public class DatabaseReadinessHealthCheckTests
{
    [Fact]
    public async Task Check_WhenDatabaseIsAvailable_ReturnsHealthy()
    {
        var check = CreateCheck(
            _ => Task.FromResult(true),
            _ => Task.FromResult(false));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Check_WhenDatabaseIsUnavailable_ReturnsUnhealthy()
    {
        var check = CreateCheck(
            _ => Task.FromResult(false),
            _ => Task.FromResult(false));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Check_WhenDatabaseHasPendingMigrations_ReturnsUnhealthy()
    {
        var check = CreateCheck(
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("The required database schema is not current.", result.Description);
    }

    [Fact]
    public async Task Check_WhenDatabaseTimesOut_ReturnsUnhealthy()
    {
        var check = CreateCheck(
            async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return true;
            },
            _ => Task.FromResult(false));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), timeout.Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task ReadinessFilter_ExcludesUnhealthyOptionalProviders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck(
                "database",
                () => HealthCheckResult.Healthy(),
                tags: [ReadinessHealthCheckOptions.RequiredTag])
            .AddCheck("payments", () => HealthCheckResult.Unhealthy(), tags: ["optional"])
            .AddCheck("sms", () => HealthCheckResult.Unhealthy(), tags: ["optional"])
            .AddCheck("email", () => HealthCheckResult.Unhealthy(), tags: ["optional"]);
        await using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();
        var options = ReadinessHealthCheckOptions.Create();

        var report = await healthCheckService.CheckHealthAsync(options.Predicate!);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal(["database"], report.Entries.Keys);
    }

    [Fact]
    public void ReadinessOptions_MapOnlyUnhealthyToServiceUnavailable()
    {
        var options = ReadinessHealthCheckOptions.Create();

        Assert.Equal(StatusCodes.Status200OK, options.ResultStatusCodes[HealthStatus.Healthy]);
        Assert.Equal(StatusCodes.Status200OK, options.ResultStatusCodes[HealthStatus.Degraded]);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, options.ResultStatusCodes[HealthStatus.Unhealthy]);
    }

    [Fact]
    public async Task ResponseWriter_DoesNotExposeExceptionDetails()
    {
        const string secret = "Host=private-db;Password=do-not-expose";
        var entry = new HealthReportEntry(
            HealthStatus.Unhealthy,
            secret,
            TimeSpan.Zero,
            new InvalidOperationException(secret),
            data: null);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry> { ["database"] = entry },
            TimeSpan.Zero);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteReadinessResponseAsync(context, report);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("\"status\":\"notReady\"", body);
        Assert.Contains("\"database\":\"unhealthy\"", body);
        Assert.DoesNotContain(secret, body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    private static DatabaseReadinessHealthCheck CreateCheck(
        Func<CancellationToken, Task<bool>> canConnect,
        Func<CancellationToken, Task<bool>> hasPendingMigrations) =>
        new(
            new StubDatabaseReadinessProbe(canConnect, hasPendingMigrations),
            NullLogger<DatabaseReadinessHealthCheck>.Instance);

    private sealed class StubDatabaseReadinessProbe(
        Func<CancellationToken, Task<bool>> canConnect,
        Func<CancellationToken, Task<bool>> hasPendingMigrations) : IDatabaseReadinessProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
            canConnect(cancellationToken);

        public Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken) =>
            hasPendingMigrations(cancellationToken);
    }
}
