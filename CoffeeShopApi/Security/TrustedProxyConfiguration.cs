using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace CoffeeShopApi.Security;

/// <summary>
/// Builds the forwarded-header policy from deployment configuration. Forwarded
/// headers remain disabled unless a host explicitly enables them and supplies a
/// trusted proxy or network.
/// </summary>
internal static class TrustedProxyConfiguration
{
    private const string SectionName = "ForwardedHeaders";
    private const string DefaultClientIpHeader = "CF-Connecting-IP";

    public static ForwardedHeadersOptions Build(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var options = new ForwardedHeadersOptions();
        if (!section.GetValue("Enabled", false))
        {
            return options;
        }

        var clientIpHeader = section["ClientIpHeader"] ?? DefaultClientIpHeader;
        if (string.IsNullOrWhiteSpace(clientIpHeader) || clientIpHeader.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ClientIpHeader must be a single HTTP header name.");
        }

        var forwardLimit = section.GetValue("ForwardLimit", 1);
        if (forwardLimit is < 1 or > 10)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ForwardLimit must be between 1 and 10.");
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardedForHeaderName = clientIpHeader;
        options.ForwardLimit = forwardLimit;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        AddKnownProxies(options, section["KnownProxies"]);
        AddKnownNetworks(options, section["KnownNetworks"]);

        if (options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:KnownProxies or {SectionName}:KnownNetworks must contain " +
                "at least one trusted entry when forwarded headers are enabled.");
        }

        return options;
    }

    private static void AddKnownProxies(ForwardedHeadersOptions options, string? configured)
    {
        foreach (var value in Split(configured))
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownProxies contains an invalid IP address: '{value}'.");
            }

            options.KnownProxies.Add(address);
        }
    }

    private static void AddKnownNetworks(ForwardedHeadersOptions options, string? configured)
    {
        foreach (var value in Split(configured))
        {
            var separator = value.LastIndexOf('/');
            if (separator <= 0 || separator == value.Length - 1 ||
                !IPAddress.TryParse(value[..separator], out var prefix) ||
                !int.TryParse(value[(separator + 1)..], out var prefixLength) ||
                prefixLength < 0 || prefixLength > prefix.GetAddressBytes().Length * 8)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownNetworks contains an invalid CIDR network: '{value}'.");
            }

            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }
    }

    private static IEnumerable<string> Split(string? configured) =>
        (configured ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
