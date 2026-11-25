using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// Rate limiting setup.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// How many account form posts one client IP may make per <see cref="AccountWindow"/>.
    /// </summary>
    private const int AccountPermitLimit = 10;

    /// <summary>
    /// The window of <see cref="AccountPermitLimit"/>.
    /// </summary>
    private static readonly TimeSpan AccountWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Limit account form posts per client IP.
    /// </summary>
    /// <remarks>
    /// The client IP is the one resolved by <see cref="ProxyExtensions.AddArrangerForwardedHeaders"/>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection AddArrangerRateLimiting(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitingPolicies.Account, context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = AccountPermitLimit, Window = AccountWindow, QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter(string.Empty));
        });
    }
}
