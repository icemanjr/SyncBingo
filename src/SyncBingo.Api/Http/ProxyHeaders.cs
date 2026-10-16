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
/// <para>
/// Behind CloudFront and an ALB (the Full profile, docs/07 §2) there are two proxies, and the client's address
/// is not the last one in <c>X-Forwarded-For</c>. There <c>ForwardedHeaders:ClientAddressHeader</c> names
/// <c>CloudFront-Viewer-Address</c> instead: CloudFront always sets it itself (a client's own copy is replaced),
/// as <c>address:port</c>, and the ALB accepts only CloudFront's addresses.
/// </para>
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

    /// <summary>Takes the client's address from the proxy, once <see cref="Enabled"/>; first, before the rate limits.</summary>
    public static IApplicationBuilder UseProxyHeaders(this IApplicationBuilder app, IConfiguration configuration)
    {
        var header = configuration.GetValue<string>("ForwardedHeaders:ClientAddressHeader");
        if (string.IsNullOrEmpty(header))
        {
            return app.UseForwardedHeaders();
        }

        return app.Use((context, next) =>
        {
            var from = context.Connection.RemoteIpAddress;
            if (from is not null
                && PrivateNetworks.Any(network => network.Contains(from))
                && ParseViewerAddress(context.Request.Headers[header].ToString()) is { } client)
            {
                context.Connection.RemoteIpAddress = client;
            }

            return next(context);
        });
    }

    /// <summary>
    /// The address in <c>address:port</c>, as CloudFront writes it: <c>198.51.100.10:46532</c>, or for IPv6
    /// <c>2001:db8::1:46532</c>, without brackets. So the port is whatever follows the last colon.
    /// </summary>
    internal static IPAddress? ParseViewerAddress(string value)
    {
        var colon = value.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(value.AsSpan(colon + 1), out _))
        {
            return null;
        }

        return IPAddress.TryParse(value.AsSpan(0, colon).Trim('[').TrimEnd(']'), out var address) ? address : null;
    }

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
