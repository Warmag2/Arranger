namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Add a currency to the arrangement, or change its exchange rate.
/// </summary>
/// <param name="Rate">The units of the currency one unit of the base currency buys, or <b>null</b> when not known.</param>
/// <param name="LossPercent">The average loss on converting to the currency, in percent.</param>
public sealed record SetExchangeRateRequest(decimal? Rate, decimal LossPercent = 0);
