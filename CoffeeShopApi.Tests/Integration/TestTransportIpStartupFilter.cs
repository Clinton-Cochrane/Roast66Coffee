using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace CoffeeShopApi.Tests.Integration;

/// <summary>
/// Gives forwarded-header integration tests control over the address of the
/// immediate transport peer without adding a production endpoint or hook.
/// </summary>
internal sealed class TestTransportIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app => next(app.Use(async (context, continuation) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Transport-IP", out var value) &&
                IPAddress.TryParse(value.ToString(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
                context.Request.Headers.Remove("X-Test-Transport-IP");
            }

            await continuation();
        }));
}
