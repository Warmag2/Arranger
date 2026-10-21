namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Change the currency balances are settled in. Clears the exchange rates.
/// </summary>
/// <param name="Currency">The ISO 4217 currency.</param>
public sealed record SetBaseCurrencyRequest(string Currency);
