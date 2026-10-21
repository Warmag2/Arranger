using System;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Grains;
using Microsoft.AspNetCore.Builder;
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
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The hub endpoint.</returns>
    public static HubEndpointConventionBuilder MapArrangerHubs(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapHub<ArrangementHub>("/" + ArrangementHubProtocol.Path);
    }
}
