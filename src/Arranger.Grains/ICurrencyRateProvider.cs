using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// Provides market exchange rates between currencies, converting one unit of a base currency into a target currency.
/// </summary>
public interface ICurrencyRateProvider
{
    /// <summary>
    /// Get the latest market rate for converting one unit of <paramref name="baseCurrency"/> into each of the given
    /// currencies.
    /// </summary>
    /// <param name="baseCurrency">The ISO 4217 currency one unit is priced in.</param>
    /// <param name="currencies">The ISO 4217 currencies to price.</param>
    /// <param name="forceRefresh"><b>true</b> to refetch the rates even when the cached ones are still fresh.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The rate for each currency that could be priced, keyed by currency. Currencies the upstream service does not
    /// cover, or that cannot be priced, are left out rather than guessed.
    /// </returns>
    Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(string baseCurrency, IReadOnlyCollection<string> currencies, bool forceRefresh, CancellationToken cancellationToken = default);
}
