using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace SyncBingo.Api.Http;

/// <summary>
/// Behind a reverse proxy (Caddy on the live site, nginx in local Compose), every request comes from the
/// proxy's address, so the rate limits would treat all players as one. With <c>ForwardedHeaders:Enabled</c>,
/// the API takes the client's address from the proxy's <c>X-Forwarded-For</c> instead, but only when the
/// request comes from a private network (where the proxy runs, next to the API). Anyone else's header is
/// ignored, so a client can't claim a fresh address to dodge a limit.
/// </summary>
internal static class ProxyHeaders
{
    /// <summary>Loopback and the private ranges container networks use (Docker, Podman, a VPC).</summary>
    private static readonly IPNetwork[] PrivateNetworks =
    [
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("fc00::/7"),
    ];

    public static bool Enabled(IConfiguration configuration) => configuration.GetValue<bool>("ForwardedHeaders:Enabled");

    public static IServiceCollection AddProxyHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // One proxy: take the address it appended (the last one), never ones the client sent before it.
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var network in PrivateNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
}
