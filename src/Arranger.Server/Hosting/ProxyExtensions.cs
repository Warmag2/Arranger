using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// Reverse proxy setup.
/// </summary>
public static class ProxyExtensions
{
    /// <summary>
    /// The configuration section listing the trusted reverse proxy addresses.
    /// </summary>
    public const string KnownProxiesSectionName = "ReverseProxy:KnownProxies";

    /// <summary>
    /// Trust <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from the configured proxies only.
    /// </summary>
    /// <remarks>
    /// Loopback is always trusted. The resulting client IP is what the rate limiter and the failed-login log use.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection AddArrangerForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var knownProxies = configuration.GetSection(KnownProxiesSectionName).Get<string[]>() ?? [];

        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
    }
}
