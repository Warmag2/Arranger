using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Grains;
using Faemiyah.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Server.Realtime;

/// <summary>
/// Registration of the SignalR hub and the grain notifier.
/// </summary>
public static class RealtimeExtensions
{
    /// <summary>
    /// Add SignalR with the Faemiyah JSON defaults, and the notifier the grains push changes through.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection AddArrangerRealtime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSignalR()
            .AddJsonProtocol(options => JsonConfiguration.ApplyDefaultJsonSerializerOptions(options.PayloadSerializerOptions));
        services.AddSingleton<IArrangementNotifier, HubArrangementNotifier>();
        return services;
    }

    /// <summary>
    /// Map the arrangement hub at <see cref="ArrangementHubProtocol.Path"/>.
    /// </summary>
    /// <remarks>
    /// WebSockets are not covered by CORS, and <c>SameSite=Lax</c> sends the cookie from sibling subdomains, so
    /// browser connections whose <c>Origin</c> is another host are refused. Clients that send no <c>Origin</c>
    /// are not browsers and cannot use the victim's cookie. Behind a proxy, it must pass the original
    /// <c>Host</c> header on.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The hub endpoint.</returns>
    public static HubEndpointConventionBuilder MapArrangerHubs(this IEndpointRouteBuilder endpoints)
    {
        var hub = endpoints.MapHub<ArrangementHub>("/" + ArrangementHubProtocol.Path);
        hub.Add(endpoint => endpoint.RequestDelegate = RefuseForeignOrigin(endpoint.RequestDelegate!));
        return hub;
    }

    private static RequestDelegate RefuseForeignOrigin(RequestDelegate next)
    {
        return context =>
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0
                && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            return next(context);
        };
    }
}
