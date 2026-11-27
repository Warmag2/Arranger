using Faemiyah.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// JSON setup for the HTTP API.
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// Apply the Faemiyah JSON defaults to minimal API requests and responses.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection ConfigureArrangerJson(this IServiceCollection services)
    {
        return services.ConfigureHttpJsonOptions(options => JsonConfiguration.ApplyDefaultJsonSerializerOptions(options.SerializerOptions));
    }
}
