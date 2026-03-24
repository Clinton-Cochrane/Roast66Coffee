using CoffeeShopApi;
using Microsoft.Extensions.Configuration;

namespace CoffeeShopApi.Tests;

public class JwtTokenSettingsTests
{
    [Fact]
    public void GetIssuerAndAudience_UseDefaultsWhenUnset()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Assert.Equal(JwtTokenSettings.DefaultIssuerAndAudience, JwtTokenSettings.GetIssuer(config));
        Assert.Equal(JwtTokenSettings.DefaultIssuerAndAudience, JwtTokenSettings.GetAudience(config));
    }

    [Fact]
    public void GetIssuerAndAudience_RespectConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CustomIssuer",
                ["Jwt:Audience"] = "CustomAudience"
            })
            .Build();

        Assert.Equal("CustomIssuer", JwtTokenSettings.GetIssuer(config));
        Assert.Equal("CustomAudience", JwtTokenSettings.GetAudience(config));
    }
}
