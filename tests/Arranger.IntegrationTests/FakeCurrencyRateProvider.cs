using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Grains;

namespace Faemiyah.Arranger.IntegrationTests;

/// <summary>
/// A controllable <see cref="ICurrencyRateProvider"/> for tests. It returns preset rates keyed by currency and ignores
/// the base currency, so tests can seed exactly the rates they expect to be applied.
/// </summary>
internal sealed class FakeCurrencyRateProvider : ICurrencyRateProvider
{
    /// <summary>
    /// The rate returned for each currency, as units of that currency per one unit of the base currency.
    /// </summary>
    public Dictionary<string, decimal> Rates { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The number of times <see cref="GetRatesAsync"/> was called.
    /// </summary>
    public int Calls { get; private set; }

    /// <summary>
    /// Whether the last call asked for a forced refresh.
    /// </summary>
    public bool LastForceRefresh { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(string baseCurrency, IReadOnlyCollection<string> currencies, bool forceRefresh, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastForceRefresh = forceRefresh;
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var currency in currencies)
        {
            if (Rates.TryGetValue(currency, out var rate))
            {
                result[currency] = rate;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, decimal>>(result);
    }
}
