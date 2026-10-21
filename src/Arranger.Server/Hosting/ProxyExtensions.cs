using System;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// Reverse proxy and rate limiting setup.
/// </summary>
public static class ProxyExtensions
{
    /// <summary>
    /// The configuration section listing the trusted reverse proxy addresses.
    /// </summary>
    public const string KnownProxiesSectionName = "ReverseProxy:KnownProxies";

    /// <summary>
    /// Trust <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from the configured proxies only, and limit account
    /// form posts per client IP.
    /// </summary>
    /// <remarks>
    /// Loopback is always trusted. The resulting client IP is what the rate limiter and the failed-login log use.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection AddArrangerProxyAndRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var knownProxies = configuration.GetSection(KnownProxiesSectionName).Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitingPolicies.Account, context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter(string.Empty));
        });

        return services;
    }
}
