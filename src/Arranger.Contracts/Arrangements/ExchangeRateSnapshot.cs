namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A currency the arrangement uses besides its base currency.
/// </summary>
/// <param name="Rate">The units of the currency one unit of the base currency buys, or <b>null</b> when not yet known.</param>
/// <param name="LossPercent">The average loss on converting to the currency, in percent.</param>
public sealed record ExchangeRateSnapshot(decimal? Rate, decimal LossPercent)
{
    /// <summary>
    /// The units of the currency one unit of the base currency actually buys: losing p % on conversion means
    /// <c>Rate × (1 − p / 100)</c>. Amounts are divided by it to get their value in the base currency.
    /// </summary>
    /// <returns>The rate used for the balances, or <b>null</b> when the rate is not known.</returns>
    public decimal? EffectiveRate()
    {
        return Rate * (1 - (LossPercent / 100));
    }
}
