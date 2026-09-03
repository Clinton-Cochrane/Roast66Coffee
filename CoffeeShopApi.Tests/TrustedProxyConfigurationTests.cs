using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using CoffeeShopApi.Security;
using Xunit;

namespace CoffeeShopApi.Tests;

public class TrustedProxyConfigurationTests
{
    [Fact]
    public void Build_DisabledConfiguration_DoesNotProcessForwardedHeaders()
    {
        var options = TrustedProxyConfiguration.Build(Configuration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "false"
        }));

        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
    }

    [Fact]
    public void Build_ParsesKnownProxiesAndNetworks()
    {
        var options = TrustedProxyConfiguration.Build(Configuration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:ClientIpHeader"] = "CF-Connecting-IP",
            ["ForwardedHeaders:KnownProxies"] = "127.0.0.1",
            ["ForwardedHeaders:KnownNetworks"] = "10.0.0.0/8",
            ["ForwardedHeaders:ForwardLimit"] = "1"
        }));

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal("CF-Connecting-IP", options.ForwardedForHeaderName);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Single(options.KnownProxies);
        Assert.Single(options.KnownNetworks);
    }

    [Fact]
    public void Build_EnabledWithoutTrustEntries_FailsClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TrustedProxyConfiguration.Build(Configuration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:Enabled"] = "true"
            })));

        Assert.Contains("KnownProxies or ForwardedHeaders:KnownNetworks", exception.Message);
    }

    [Fact]
    public void Build_InvalidNetwork_FailsClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TrustedProxyConfiguration.Build(Configuration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:Enabled"] = "true",
                ["ForwardedHeaders:KnownNetworks"] = "not-a-network"
            })));

        Assert.Contains("invalid CIDR network", exception.Message);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
