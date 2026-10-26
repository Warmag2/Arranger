using System;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// Registration for the currency rate cache and its upstream HTTP client.
/// </summary>
public static class CurrencyRateServiceCollectionExtensions
{
    /// <summary>
    /// The base address of the free upstream exchange rate service (no API key required).
    /// </summary>
    private static readonly Uri UpstreamBaseAddress = new("https://open.er-api.com/");

    /// <summary>
    /// Register the process-wide currency rate cache and the HTTP client it uses to fetch rates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection AddArrangerCurrencyRates(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient(CurrencyRateProvider.HttpClientName, client =>
        {
            client.BaseAddress = UpstreamBaseAddress;
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<ICurrencyRateProvider, CurrencyRateProvider>();
        return services;
    }
}
