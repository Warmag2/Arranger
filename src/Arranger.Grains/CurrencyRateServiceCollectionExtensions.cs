using System;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// Registration for the currency rate cache and its upstream HTTP client.
/// </summary>
public static class CurrencyRateServiceCollectionExtensions
{
    /// <summary>
    /// How long one request to the upstream service may take.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

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
            client.Timeout = RequestTimeout;
        });
        services.AddSingleton<ICurrencyRateProvider, CurrencyRateProvider>();
        return services;
    }
}
