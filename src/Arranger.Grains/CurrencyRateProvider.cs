using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// A process-wide cache of market exchange rates, fetched lazily from a free upstream service.
/// </summary>
/// <remarks>
/// The rates are held in memory for the lifetime of the process and refetched only when they are older than
/// <see cref="RefreshInterval"/> and someone asks for them, or when a refresh is forced. A failed fetch keeps the last
/// known rates. One upstream pivot table (per one unit of the pivot currency) is held and cross rates are computed from
/// it, so adding a currency costs no extra request. A single-entry gate collapses concurrent fetches into one request.
/// </remarks>
public sealed class CurrencyRateProvider : ICurrencyRateProvider, IDisposable
{
    /// <summary>
    /// The name of the configured <see cref="HttpClient"/> used to reach the upstream service.
    /// </summary>
    public const string HttpClientName = "CurrencyRates";

    /// <summary>
    /// The currency every upstream rate is quoted against. It is the most widely covered base.
    /// </summary>
    private const string PivotCurrency = "USD";

    /// <summary>
    /// How old the cached rates may be before the next request refetches them.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CurrencyRateProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, decimal>? _perPivot;
    private DateTimeOffset _fetchedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyRateProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public CurrencyRateProvider(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<CurrencyRateProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Dispose();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(string baseCurrency, IReadOnlyCollection<string> currencies, bool forceRefresh, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseCurrency);
        ArgumentNullException.ThrowIfNull(currencies);

        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (currencies.Count == 0)
        {
            return result;
        }

        var table = await GetTableAsync(forceRefresh, cancellationToken);
        if (table == null || !table.TryGetValue(baseCurrency, out var basePerPivot) || basePerPivot <= 0)
        {
            return result;
        }

        foreach (var currency in currencies)
        {
            if (!table.TryGetValue(currency, out var currencyPerPivot) || currencyPerPivot <= 0)
            {
                continue;
            }

            // One unit of the base currency buys this many units of the target currency.
            var rate = decimal.Round(currencyPerPivot / basePerPivot, ExpenseRules.RateDecimals, MidpointRounding.AwayFromZero);
            if (rate > 0 && rate <= ExpenseRules.MaxRate)
            {
                result[currency] = rate;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, decimal>?> GetTableAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && IsFresh())
        {
            return _perPivot;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have refreshed the rates while this one waited for the gate.
            if (!forceRefresh && IsFresh())
            {
                return _perPivot;
            }

            var fetched = await FetchAsync(cancellationToken);
            if (fetched is { Count: > 0 })
            {
                _perPivot = fetched;
                _fetchedAt = _timeProvider.GetUtcNow();
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Could not fetch currency rates from the upstream service. Keeping the last known rates.");
        }
        finally
        {
            _gate.Release();
        }

        return _perPivot;
    }

    private bool IsFresh()
    {
        return _perPivot != null && _timeProvider.GetUtcNow() - _fetchedAt < RefreshInterval;
    }

    private async Task<Dictionary<string, decimal>?> FetchAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync($"v6/latest/{PivotCurrency}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UpstreamResponse>(cancellationToken);
        if (payload is null || !string.Equals(payload.Result, "success", StringComparison.Ordinal) || payload.Rates is null)
        {
            _logger.LogWarning("The currency rate service returned an unexpected payload (result '{Result}').", payload?.Result);
            return null;
        }

        var table = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (code, value) in payload.Rates)
        {
            if (Currencies.IsKnown(code) && value > 0)
            {
                table[code] = value;
            }
        }

        return table;
    }

    /// <summary>
    /// The shape of the open.er-api.com latest-rates response that this provider reads.
    /// </summary>
    private sealed record UpstreamResponse(
        [property: JsonPropertyName("result")] string? Result,
        [property: JsonPropertyName("base_code")] string? BaseCode,
        [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);
}
